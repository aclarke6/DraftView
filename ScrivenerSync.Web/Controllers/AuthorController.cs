using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrivenerSync.Domain.Entities;
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
        var author = await GetAuthorAsync();
        if (author is null)
            return RedirectToAction("Index", "Reader");

        var projects         = await projectRepo.GetAllAsync();
        var active           = await projectRepo.GetReaderActiveProjectAsync();
        var publishedChapters = active is not null
            ? await publicationService.GetPublishedChaptersAsync(active.Id)
            : new List<Section>();
        var failures  = await dashboardService.GetEmailHealthSummaryAsync();
        var readers   = await userRepo.GetAllBetaReadersAsync();

        return View(new DashboardViewModel
        {
            ActiveProject     = active,
            AllProjects       = projects,
            PublishedSections = publishedChapters,
            EmailFailures     = failures,
            ActiveReaderCount = readers.Count(r => r.IsActive && !r.IsSoftDeleted)
        });
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
    // Sections list with chapter publish buttons
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Sections(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        var sections = await sectionRepo.GetByProjectIdAsync(projectId);
        var sorted   = SortDepthFirst(sections);

        // Pre-compute which folders can be published
        var publishable = new HashSet<Guid>();
        foreach (var (s, _) in sorted.Where(x => x.Section.NodeType == NodeType.Folder))
        {
            if (await publicationService.CanPublishAsync(s.Id))
                publishable.Add(s.Id);
        }

        ViewBag.Project    = project;
        ViewBag.Publishable = publishable;
        return View(sorted);
    }

    // ---------------------------------------------------------------------------
    // Chapter publish / unpublish
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishChapter(Guid chapterId, Guid projectId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        try
        {
            await publicationService.PublishChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter published.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Sections", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishChapter(Guid chapterId, Guid projectId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await publicationService.UnpublishChapterAsync(chapterId, author.Id);
        TempData["Success"] = "Chapter unpublished.";
        return RedirectToAction("Sections", new { projectId });
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
            var policy = model.NeverExpires ? ExpiryPolicy.AlwaysOpen : ExpiryPolicy.ExpiresAt;
            await userService.IssueInvitationAsync(model.Email, policy, model.ExpiresAt, author.Id);
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
    public async Task<IActionResult> ReactivateReader(Guid userId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await userService.ReactivateUserAsync(userId, author.Id);
        TempData["Success"] = "Reader reactivated.";
        return RedirectToAction("Readers");
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
    // Section detail with comments (author view)
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Section(Guid id)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        var s = await sectionRepo.GetByIdAsync(id);
        if (s is null) return NotFound();

        var comments = await GetCommentService().GetThreadsForSectionAsync(id, author.Id);
        var events   = await GetReadEventRepo().GetBySectionIdAsync(id);

        return View(new SectionViewModel
        {
            Section   = s,
            Comments  = comments,
            ReadCount = events.Count
        });
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private async Task<User?> GetAuthorAsync()
    {
        var email = User.Identity?.Name;
        if (email is null)
            return null;
        var user = await userRepo.GetByEmailAsync(email);
        return user?.Role == Domain.Enumerations.Role.Author ? user : null;
    }

    private IUnitOfWork GetUnitOfWork() =>
        HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

    private ICommentService GetCommentService() =>
        HttpContext.RequestServices.GetRequiredService<ICommentService>();

    private IReadEventRepository GetReadEventRepo() =>
        HttpContext.RequestServices.GetRequiredService<IReadEventRepository>();

    private static IReadOnlyList<(Section Section, int Depth)> SortDepthFirst(
        IReadOnlyList<Section> sections)
    {
        var root   = Guid.Empty;
        var lookup = new Dictionary<Guid, List<Section>>();

        foreach (var s in sections)
        {
            var key = s.ParentId ?? root;
            if (!lookup.ContainsKey(key))
                lookup[key] = new List<Section>();
            lookup[key].Add(s);
        }

        foreach (var key in lookup.Keys.ToList())
            lookup[key] = lookup[key].OrderBy(s => s.SortOrder).ToList();

        var result = new List<(Section, int)>();

        void Walk(Guid parentId, int depth)
        {
            if (!lookup.TryGetValue(parentId, out var children)) return;
            foreach (var child in children)
            {
                result.Add((child, depth));
                Walk(child.Id, depth + 1);
            }
        }

        Walk(root, 0);
        return result;
    }
}

