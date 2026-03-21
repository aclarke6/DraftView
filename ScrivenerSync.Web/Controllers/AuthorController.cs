using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;
using ScrivenerSync.Web.Models;

namespace ScrivenerSync.Web.Controllers;

[Authorize]
public class AuthorController(
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    IPublicationService publicationService,
    IUserService userService,
    IDashboardService dashboardService,
    ISyncService syncService,
    IUserRepository userRepo,
    ILogger<AuthorController> logger) : Controller
{
    // ---------------------------------------------------------------------------
    // Dashboard
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var author   = await GetAuthorAsync();
        if (author is null) return Forbid();

        var projects  = await projectRepo.GetAllAsync();
        var active    = await projectRepo.GetReaderActiveProjectAsync();
        var published = active is not null
            ? await sectionRepo.GetPublishedByProjectIdAsync(active.Id)
            : new List<Domain.Entities.Section>();
        var failures  = await dashboardService.GetEmailHealthSummaryAsync();
        var readers   = await userRepo.GetAllBetaReadersAsync();

        var vm = new DashboardViewModel
        {
            ActiveProject    = active,
            AllProjects      = projects,
            PublishedSections = published,
            EmailFailures    = failures,
            ActiveReaderCount = readers.Count(r => r.IsActive && !r.IsSoftDeleted)
        };

        return View(vm);
    }

    // ---------------------------------------------------------------------------
    // Sync
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(Guid projectId)
    {
        try
        {
            await syncService.ParseProjectAsync(projectId);
            TempData["Success"] = "Project synced successfully.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed for project {ProjectId}", projectId);
            TempData["Error"] = $"Sync failed: {ex.Message}";
        }
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Project activation
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateProject(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        // Deactivate any currently active project
        var current = await projectRepo.GetReaderActiveProjectAsync();
        if (current is not null && current.Id != projectId)
            current.DeactivateForReaders();

        project.ActivateForReaders();
        await GetUnitOfWork().SaveChangesAsync();

        TempData["Success"] = $"{project.Name} is now active for readers.";
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateProject(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        project.DeactivateForReaders();
        await GetUnitOfWork().SaveChangesAsync();

        TempData["Success"] = $"{project.Name} is now inactive for readers.";
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Publication
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid sectionId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await publicationService.PublishAsync(sectionId, author.Id);
        TempData["Success"] = "Section published.";
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(Guid sectionId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await publicationService.UnpublishAsync(sectionId, author.Id);
        TempData["Success"] = "Section unpublished.";
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Readers
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Readers()
    {
        var readers = await userRepo.GetAllBetaReadersAsync();
        return View(readers);
    }

    [HttpGet]
    public IActionResult InviteReader() => View(new InviteReaderViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteReader(InviteReaderViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        try
        {
            var policy  = model.NeverExpires ? ExpiryPolicy.AlwaysOpen : ExpiryPolicy.ExpiresAt;
            await userService.IssueInvitationAsync(
                model.Email, policy, model.ExpiresAt, author.Id);
            TempData["Success"] = $"Invitation sent to {model.Email}.";
            return RedirectToAction("Readers");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateReader(Guid userId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await userService.DeactivateUserAsync(userId, author.Id);
        TempData["Success"] = "Reader deactivated.";
        return RedirectToAction("Readers");
    }

    // ---------------------------------------------------------------------------
    // Section detail with comments
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Section(Guid id)
    {
        var author  = await GetAuthorAsync();
        if (author is null) return Forbid();

        var section  = await sectionRepo.GetByIdAsync(id);
        if (section is null) return NotFound();

        var comments = await GetCommentService().GetThreadsForSectionAsync(id, author.Id);
        var events   = await GetReadEventRepo().GetBySectionIdAsync(id);

        return View(new SectionViewModel
        {
            Section   = section,
            Comments  = comments,
            ReadCount = events.Count
        });
    }

    // ---------------------------------------------------------------------------
    // All sections
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Sections(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        var sections = await sectionRepo.GetByProjectIdAsync(projectId);
        ViewBag.Project = project;
        return View(sections);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishSection(Guid sectionId, Guid projectId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        try
        {
            await publicationService.PublishAsync(sectionId, author.Id);
            TempData["Success"] = "Section published.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Sections", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishSection(Guid sectionId, Guid projectId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await publicationService.UnpublishAsync(sectionId, author.Id);
        TempData["Success"] = "Section unpublished.";
        return RedirectToAction("Sections", new { projectId });
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------
    private async Task<Domain.Entities.User?> GetAuthorAsync() =>
        await userRepo.GetAuthorAsync();

    private IUnitOfWork GetUnitOfWork() =>
        HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

    private ICommentService GetCommentService() =>
        HttpContext.RequestServices.GetRequiredService<ICommentService>();

    private IReadEventRepository GetReadEventRepo() =>
        HttpContext.RequestServices.GetRequiredService<IReadEventRepository>();
}

