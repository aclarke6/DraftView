using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;
using ScrivenerSync.Web.Models;

namespace ScrivenerSync.Web.Controllers;

[Authorize]
public class ReaderController(
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ICommentService commentService,
    IReadingProgressService progressService,
    IUserRepository userRepo,
    ILogger<ReaderController> logger) : Controller
{
    // ---------------------------------------------------------------------------
    // Reading index - table of contents for active project
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Index()
    {
        var project = await projectRepo.GetReaderActiveProjectAsync();
        if (project is null)
            return View("NoActiveProject");

        var sections = await sectionRepo.GetPublishedByProjectIdAsync(project.Id);
        return View(sections);
    }

    // ---------------------------------------------------------------------------
    // Read a section
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Read(Guid id)
    {
        var section = await sectionRepo.GetByIdAsync(id);
        if (section is null || !section.IsPublished)
            return NotFound();

        var user = await GetCurrentUserAsync();
        if (user is null) return Forbid();

        // Record that the reader opened this section
        await progressService.RecordOpenAsync(id, user.Id);

        var project  = await projectRepo.GetReaderActiveProjectAsync();
        var allSections = project is not null
            ? await sectionRepo.GetPublishedByProjectIdAsync(project.Id)
            : new List<Domain.Entities.Section>();

        var comments = await commentService.GetThreadsForSectionAsync(id, user.Id);

        return View(new ReadingViewModel
        {
            Section          = section,
            Comments         = comments,
            TableOfContents  = allSections
        });
    }

    // ---------------------------------------------------------------------------
    // Add a comment
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(AddCommentViewModel model)
    {
        if (!ModelState.IsValid)
            return RedirectToAction("Read", new { id = model.SectionId });

        var user = await GetCurrentUserAsync();
        if (user is null) return Forbid();

        var visibility = model.IsPrivate ? Visibility.Private : Visibility.Public;

        try
        {
            if (model.ParentCommentId.HasValue)
            {
                await commentService.CreateReplyAsync(
                    model.ParentCommentId.Value, user.Id, model.Body, visibility);
            }
            else
            {
                await commentService.CreateRootCommentAsync(
                    model.SectionId, user.Id, model.Body, visibility);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add comment for user {UserId}", user.Id);
            TempData["Error"] = "Failed to save comment.";
        }

        return RedirectToAction("Read", new { id = model.SectionId });
    }

    // ---------------------------------------------------------------------------
    // Delete own comment
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid commentId, Guid sectionId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return Forbid();

        await commentService.SoftDeleteCommentAsync(commentId, user.Id);
        return RedirectToAction("Read", new { id = sectionId });
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------
    private async Task<Domain.Entities.User?> GetCurrentUserAsync()
    {
        var email = User.Identity?.Name;
        if (email is null) return null;
        return await userRepo.GetByEmailAsync(email);
    }
}
