using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using DraftView.Web.Infrastructure;
namespace DraftView.Web.Controllers;

#pragma warning disable CS9107, CS9113
[Authorize(Policy = "RequireAuthorPolicy")]
public class AuthorController(
    IProjectRepository projectRepo,
    IPublicationService publicationService,
    IUserService userService,
    IDashboardService dashboardService,
    IUserRepository userRepo,
    IProjectDiscoveryService discoveryService,
    ISyncProgressTracker progressTracker,
    ISyncOrchestrationService syncOrchestrationService,
    IVersioningService versioningService,
    IImportService importService,
    ISectionTreeService sectionTreeService,
    ILogger<AuthorController> logger,
    IAccessRequestService accessRequestService,
    IAccessRequestRepository accessRequestRepo,
    IUserPreferencesRepository userPreferencesRepo,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ISectionManagementService sectionManagementService,
    IProjectManagementService projectManagementService,
    IContentNavigationService contentNavigationService,
    IReaderManagementService readerManagementService,
    IManualUploadService manualUploadService,
    IManualChapterRepository manualChapterRepo,
    IManualChapterVersionRepository manualChapterVersionRepo) : BaseController(userRepo)
{
    // ---------------------------------------------------------------------------
    // Dashboard
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var author = await TryGetCurrentAuthorAsync();
        if (author is null)
            return RedirectToAction("Index", "Reader");

        var projects          = await projectRepo.GetAllAsync();
        var active            = await projectRepo.GetReaderActiveProjectAsync();
        var publishedChapters = active is not null
            ? await publicationService.GetPublishedChaptersAsync(active.Id) : [];
        var failures      = await dashboardService.GetEmailHealthSummaryAsync();
        var readers       = await userRepo.GetAllBetaReadersAsync();
        var notifications = await dashboardService.GetNotificationsAsync(author.Id);

        return View(new DashboardViewModel
        {
            ActiveProject     = active,
            AllProjects       = projects,
            PublishedSections = publishedChapters,
            EmailFailures     = failures,
            ActiveReaderCount = readers.Count(r => r.IsActive && !r.IsSoftDeleted),
            Notifications     = notifications
        });
    }

    // ---------------------------------------------------------------------------
    // Notification dismiss
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissNotification(Guid notificationId)
    {
        await dashboardService.DismissNotificationAsync(notificationId);
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllNotifications()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();
        
        await dashboardService.DismissAllNotificationsAsync(author.Id);
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Sync
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(Guid projectId)
    {
        await syncOrchestrationService.StartSyncAsync(projectId);
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> GetSyncStatus(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        var progress = progressTracker.Get(projectId);

        return Json(new
        {
            status            = project.SyncStatus.ToString(),
            errorMessage      = project.SyncErrorMessage,
            sectionsProcessed = progress?.SectionsProcessed ?? 0,
            currentSection    = progress?.CurrentSection ?? string.Empty,
            filesDownloaded   = progress?.FilesDownloaded ?? 0,
            totalFiles        = progress?.TotalFiles ?? 0
        });
    }

    // ---------------------------------------------------------------------------
    // Project activation
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateProject(Guid projectId)
    {
        try
        {
            await projectManagementService.SetActiveProjectAsync(projectId);
            TempData["Success"] = "Project activated for readers.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateProject(Guid projectId)
    {
        try
        {
            await projectManagementService.DeactivateProjectAsync(projectId);
            TempData["Success"] = "Project deactivated for readers.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Book discovery — open / close
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenBook(Guid projectId, string brief)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        project.Open(brief);
        await GetUnitOfWork().SaveChangesAsync();

        TempData["Success"] = $"\"{project.Name}\" is now open for reader discovery.";
        return RedirectToAction("Publishing", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseBook(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        project.Close();
        await accessRequestService.BulkDeclineOnRevokeAsync(projectId);
        await GetUnitOfWork().SaveChangesAsync();

        TempData["Success"] = $"\"{project.Name}\" is now closed to new readers.";
        return RedirectToAction("Publishing", new { projectId });
    }

    // ---------------------------------------------------------------------------
    // Book requests — view / approve / decline
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> BookRequests(Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        var pending = await accessRequestRepo.GetPendingByProjectIdAsync(projectId);
        var rows = new List<AccessRequestRowViewModel>();
        foreach (var req in pending)
        {
            var reader = await userRepo.GetByIdAsync(req.ReaderId);
            var prefs  = await userPreferencesRepo.GetByUserIdAsync(req.ReaderId);
            rows.Add(new AccessRequestRowViewModel
            {
                Request           = req,
                ReaderDisplayName = reader?.DisplayName ?? "Unknown",
                ReaderBio         = prefs?.ReaderBio,
                ReaderPace        = prefs?.ReaderPace
            });
        }

        return View(new BookRequestsViewModel { Project = project, Requests = rows });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(Guid requestId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await accessRequestService.ApproveRequestAsync(requestId, author.Id);
        TempData["Success"] = "Request approved and reader notified.";
        return RedirectToAction("BookRequests", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineRequest(Guid requestId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await accessRequestService.DeclineRequestAsync(requestId, author.Id);
        TempData["Success"] = "Request declined.";
        return RedirectToAction("BookRequests", new { projectId });
    }

    // ---------------------------------------------------------------------------
    // Sections list
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Sections(Guid projectId)
    {
        var summary = await sectionManagementService.GetSectionsSummaryAsync(projectId);
        if (summary is null) return NotFound();

        ViewBag.Project     = summary.Project;
        ViewBag.Publishable = summary.Publishable;
        ViewBag.ChapterHasChanges = summary.ChapterHasChanges;
        ViewBag.ClassificationMap = summary.ClassificationMap;
        return View(summary.SortedSections.Select(r => (r.Section, r.Depth)).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSection(
        Guid projectId, string title, NodeType nodeType, Guid? parentId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await sectionTreeService.CreateSectionAsync(
                projectId,
                title,
                nodeType,
                parentId,
                sortOrder: null,
                author.Id);

            TempData["Success"] = "Section created.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Sections", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveSection(
        Guid sectionId, Guid projectId, Guid? newParentId, int newSortOrder)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await sectionTreeService.MoveSectionAsync(
                sectionId,
                newParentId,
                newSortOrder,
                author.Id);

            return Ok();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return BadRequest(ex.Message);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSection(Guid sectionId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await sectionTreeService.DeleteSectionAsync(sectionId, author.Id);
            TempData["Success"] = "Section deleted.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Sections", new { projectId });
    }

    // ---------------------------------------------------------------------------
    // Publishing Page
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Publishing(Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        var chapterData = await contentNavigationService.BuildPublishingChapterDataAsync(projectId, project.ProjectType);
        var pendingCount = project.IsOpen
            ? await accessRequestRepo.GetPendingCountByProjectIdAsync(projectId)
            : 0;

        return View(new PublishingPageViewModel
        {
            Project = project,
            Chapters = chapterData.Select(MapChapterData).ToList(),
            PendingRequestCount = pendingCount
        });
    }

    // ---------------------------------------------------------------------------
    // Chapter publish / unpublish
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await publicationService.PublishChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter published.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return Redirect(Url.Action("Sections", new
        {
            projectId
        }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await publicationService.UnpublishChapterAsync(chapterId, author.Id);
        TempData["Success"] = "Chapter unpublished.";
        return Redirect(Url.Action("Sections", new
        {
            projectId
        }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RepublishChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.RepublishChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter republished. Readers will see the updated content.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Sections", new { projectId }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.LockChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter locked.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.UnlockChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter unlocked.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleChapter(Guid chapterId, Guid projectId, DateTime scheduledAt)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.ScheduleChapterAsync(chapterId, author.Id, scheduledAt.ToUniversalTime());
            TempData["Success"] = "Publish date set.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearChapterSchedule(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.ClearScheduleAsync(chapterId, author.Id);
            TempData["Success"] = "Publish date cleared.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RepublishDocument(Guid sectionId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.RepublishSectionAsync(sectionId, author.Id);
            TempData["Success"] = "Document republished.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersion(Guid versionId, Guid sectionId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.DeleteVersionAsync(versionId, sectionId);
            TempData["Success"] = "Version deleted.";
        }
        catch (InvariantViolationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteVersion failed for version {VersionId}", versionId);
            TempData["Error"] = "Unable to delete version. Please try again.";
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeDocument(Guid sectionId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await versioningService.RevokeLatestVersionAsync(sectionId, author.Id);
            TempData["Success"] = "Version revoked.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return Redirect(Url.Action("Publishing", new { projectId }) + "#section-" + sectionId);
    }

    // ---------------------------------------------------------------------------
    // Manual Upload
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> UploadScene(Guid projectId, Guid? parentChapterId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        return View(new UploadSceneViewModel
        {
            ProjectId       = projectId,
            ParentChapterId = parentChapterId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadScene(UploadSceneViewModel model)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var section = await sectionTreeService.GetOrCreateForUploadAsync(
                model.ProjectId,
                model.SceneTitle,
                model.ParentChapterId,
                sortOrder: null);

            await using var stream = model.File!.OpenReadStream();
            await importService.ImportAsync(
                model.ProjectId,
                section.Id,
                stream,
                model.File.FileName,
                author.Id);

            TempData["Success"] = $"\"{model.SceneTitle}\" uploaded successfully.";
        }
        catch (UnsupportedFileTypeException ex)
        {
            TempData["Error"] = $"Unsupported file type: {ex.Extension}. Only RTF files are supported.";
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return View(model);
        }

        return Redirect(Url.Action("Sections", new { projectId = model.ProjectId })
            + (model.ParentChapterId.HasValue ? "#section-" + model.ParentChapterId : string.Empty));
    }

    // ---------------------------------------------------------------------------
    // Readers
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Readers()
    {
        var rows = await readerManagementService.GetReaderSummaryAsync();
        return View(rows.Select(r => new ReaderRowViewModel
        {
            Id                   = r.Id,
            DisplayName          = r.DisplayName,
            Email                = string.Empty,
            Status               = r.Status,
            ActivatedAt          = r.ActivatedAt,
            HasPendingInvitation = r.HasPendingInvitation
        }).ToList());
    }

    [HttpGet]
    public IActionResult InviteReader() => View(new InviteReaderViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteReader(InviteReaderViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            DateTime? expiresAtUtc = null;

            if (!model.NeverExpires)
            {
                if (!model.ExpiresAt.HasValue)
                {
                    ModelState.AddModelError(nameof(model.ExpiresAt), "Please choose an expiry date.");
                    return View(model);
                }

                expiresAtUtc = DateTime.SpecifyKind(model.ExpiresAt.Value, DateTimeKind.Local).ToUniversalTime();
            }

            var policy = model.NeverExpires ? ExpiryPolicy.AlwaysOpen : ExpiryPolicy.ExpiresAt;
            await userService.IssueInvitationAsync(model.Email, model.DisplayName, policy, expiresAtUtc, author.Id);

            TempData["Success"] = "Invitation sent.";
            return RedirectToAction("Readers");
        }
        catch (InvariantViolationException ex)
        {
            logger.LogWarning(ex, "InviteReader validation failure by author {AuthorId}", author.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InviteReader operational failure by author {AuthorId}", author.Id);
            return RedirectToAction("Error", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateReader(Guid userId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await userService.ReactivateUserAsync(userId, author.Id);
        TempData["Success"] = "Reader reactivated.";
        return RedirectToAction("Readers");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvitation(Guid userId, CancellationToken ct = default)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await userService.ResendInvitationAsync(userId, author.Id, ct);
            TempData["Success"] = "Invitation resent.";
            return RedirectToAction("Readers");
        }
        catch (InvariantViolationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ResendInvitation operational failure for reader {ReaderId}", userId);
            return RedirectToAction("Error", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateReader(Guid userId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await userService.DeactivateUserAsync(userId, author.Id);
        TempData["Success"] = "Reader deactivated.";
        return RedirectToAction("Readers");
    }

    // ---------------------------------------------------------------------------
    // Section detail with comments (author view)
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Section(Guid id)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var detail = await sectionManagementService.GetSectionDetailAsync(id, author.Id);
        if (detail is null) return NotFound();

        return View(new SectionViewModel
        {
            Section            = detail.Section,
            ChapterTitle       = detail.ChapterTitle,
            Comments           = detail.Comments,
            ReadCount          = detail.ReadCount,
            CommentAuthorNames = detail.CommentAuthorNames
        });
    }

    // ---------------------------------------------------------------------------
    // Author comment reply and status
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToComment(Guid parentCommentId, Guid sectionId, string body)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();
        
        try
        {
            await GetCommentService().CreateReplyAsync(
                parentCommentId, author.Id, body, Domain.Enumerations.Visibility.Public);
            TempData["Success"] = "Reply posted.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Section", new { id = sectionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommentStatus(Guid commentId, Guid sectionId, Domain.Enumerations.CommentStatus status)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();
        
        try
        {
            await GetCommentService().SetCommentStatusAsync(commentId, author.Id, status);
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Section", new { id = sectionId });
    }

    // ---------------------------------------------------------------------------
    // Project removal
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProject(Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await projectManagementService.SoftDeleteProjectAsync(projectId);
            TempData["Success"] = "Project removed. You can re-add it from Add Project.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Projects discovery
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Projects()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> DiscoverProjects(CancellationToken ct)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return Unauthorized();

        var discovered = await discoveryService.DiscoverAsync(author.Id, ct);
        return Json(discovered.Select(p => new
        {
            p.Name,
            syncRootId = p.SyncRootId,
            vaultName  = Path.GetFileName(p.DropboxPath),
            p.AlreadyAdded
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProjects(List<string> selectedUuids)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        if (selectedUuids is null || selectedUuids.Count == 0)
        {
            TempData["Error"] = "No projects selected.";
            return RedirectToAction("Projects");
        }

        var result = await projectManagementService.AddDiscoveredProjectsAsync(selectedUuids, author.Id);

        TempData["Success"] = result.AddedCount == 1
            ? $"{result.SingleAddedProjectName} added successfully."
            : $"{result.AddedCount} projects added successfully.";

        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Reader project access management
    // ---------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> ManageReaderAccess(Guid readerId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var data = await readerManagementService.GetReaderAccessDataAsync(readerId);
        if (data is null) return NotFound();

        return View(new ReaderAccessViewModel
        {
            ReaderId              = data.ReaderId,
            DisplayName           = data.DisplayName,
            Email                 = string.Empty,
            Status                = data.Status,
            ProjectsWithAccess    = [.. data.ProjectsWithAccess],
            ProjectsWithoutAccess = [.. data.ProjectsWithoutAccess]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReaderAccess(
        Guid readerId, List<Guid> grantIds, List<Guid> revokeIds)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await readerManagementService.UpdateReaderAccessAsync(readerId, grantIds, revokeIds, author.Id);
        TempData["Success"] = "Project access updated.";
        return RedirectToAction("Readers");
    }

    // ---------------------------------------------------------------------------
    // Soft-delete reader (bin)
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDeleteReader(Guid userId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        await readerManagementService.SoftDeleteReaderAsync(userId, author.Id);
        TempData["Success"] = "Reader removed.";
        return RedirectToAction("Readers");
    }

    // ---------------------------------------------------------------------------
    // Reader impersonation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Signs the author in as the given active beta reader, preserving an
    /// ImpersonatorEmail claim so the session can be restored via ExitImpersonation.
    /// All non-GET requests are blocked by ReadOnlyImpersonationFilter while active.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImpersonateReader(Guid userId)
    {
        var reader = await userRepo.GetByIdAsync(userId);
        if (reader is null)
            return NotFound();

        if (reader.Role != Role.BetaReader)
        {
            TempData["Error"] = "Only beta readers can be viewed in Reader View.";
            return RedirectToAction("Readers");
        }

        var identityUser = await userManager.FindByEmailAsync(reader.Email);
        if (identityUser is null)
        {
            TempData["Error"] = "This reader has not yet accepted their invitation.";
            return RedirectToAction("Readers");
        }

        var principal = await signInManager.CreateUserPrincipalAsync(identityUser);
        var identity = (System.Security.Claims.ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new System.Security.Claims.Claim(ImpersonationClaims.ImpersonatorEmail, User.Identity!.Name!));
        identity.AddClaim(new System.Security.Claims.Claim(ImpersonationClaims.ImpersonatedDisplayName, reader.DisplayName));

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);

        return RedirectToAction("Dashboard", "Reader");
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------
    private IUnitOfWork GetUnitOfWork() =>
        HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

    private ICommentService GetCommentService() =>
        HttpContext.RequestServices.GetRequiredService<ICommentService>();

    private static PublishingChapterViewModel MapChapterData(PublishingChapterData d) =>
        new()
        {
            Chapter              = d.Chapter,
            HasChanges           = d.HasChanges,
            Classification       = d.Classification,
            CanRevoke            = d.CanRevoke,
            ShowDocumentControls = d.ShowDocumentControls,
            Documents            = d.Documents.Select(MapDocumentData).ToList()
        };

    private static PublishingDocumentViewModel MapDocumentData(PublishingDocumentData d) =>
        new()
        {
            Document             = d.Document,
            CurrentVersionNumber = d.CurrentVersionNumber,
            CurrentVersionLabel  = d.CurrentVersionLabel,
            HasChanges           = d.HasChanges,
            Classification       = d.Classification,
            CanRevoke            = d.CanRevoke,
            VersionHistory       = d.VersionHistory.Select(v => new VersionHistoryItem
            {
                VersionId      = v.VersionId,
                VersionNumber  = v.VersionNumber,
                VersionLabel   = v.VersionLabel,
                CreatedAt      = v.CreatedAt,
                Classification = v.Classification,
                CanDelete      = v.CanDelete
            }).ToList(),
            ShowVersionHistory   = d.ShowVersionHistory,
            RetentionLimit       = d.RetentionLimit
        };

    // ---------------------------------------------------------------------------
    // Manual Chapters
    // ---------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> ManualChapters(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();
        if (project.ProjectType != ProjectType.Manual) return BadRequest();

        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var chapters  = await manualChapterRepo.GetByProjectAsync(projectId, author.Id);
        var versionCounts = new Dictionary<Guid, int>();
        foreach (var ch in chapters)
        {
            var versions = await manualChapterVersionRepo.GetByChapterAsync(ch.Id);
            versionCounts[ch.Id] = versions.Count;
        }

        var vm = new ManualChaptersViewModel
        {
            ProjectId   = projectId,
            ProjectName = project.Name,
            Chapters    = chapters.OrderBy(c => c.SortOrder).Select(c => new ManualChapterRowViewModel
            {
                Id               = c.Id,
                Title            = c.Title,
                SortOrder        = c.SortOrder,
                OriginalFileName = c.OriginalFileName,
                UploadedAt       = c.UploadedAt,
                IsPublished      = c.LinkedSectionId.HasValue,
                VersionCount     = versionCounts.GetValueOrDefault(c.Id, 0)
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadManualChapter(UploadManualChapterViewModel model)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a title and a file.";
            return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
        }

        try
        {
            await using var stream = model.File!.OpenReadStream();
            await manualUploadService.UploadChapterAsync(
                model.ProjectId, author.Id, model.Title,
                stream, model.File.FileName);

            TempData["Success"] = $"\"{model.Title}\" uploaded successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasteManualChapter(PasteManualChapterViewModel model)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a title and content.";
            return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
        }

        try
        {
            await manualUploadService.UploadChapterFromPasteAsync(
                model.ProjectId, author.Id, model.Title, model.Content);

            TempData["Success"] = $"\"{model.Title}\" saved successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishManualChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await manualUploadService.PublishChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter published and now visible to readers.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditManualChapter(EditManualChapterViewModel model)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide content.";
            return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
        }

        try
        {
            await manualUploadService.EditChapterAsync(model.ChapterId, author.Id, model.Content);
            TempData["Success"] = "Chapter updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceManualChapter(ReplaceManualChapterViewModel model)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a file.";
            return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
        }

        try
        {
            await using var stream = model.File!.OpenReadStream();
            await manualUploadService.ReplaceChapterAsync(
                model.ChapterId, author.Id, stream, model.File.FileName);

            TempData["Success"] = "Chapter file replaced.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteManualChapter(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await manualUploadService.DeleteChapterAsync(chapterId, author.Id);
            TempData["Success"] = "Chapter deleted.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderManualChapters(Guid projectId, List<Guid> orderedIds)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await manualUploadService.ReorderChaptersAsync(projectId, author.Id, orderedIds);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> ManualChapterHistory(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var chapter = await manualChapterRepo.GetByIdAsync(chapterId, author.Id);
        if (chapter is null) return NotFound();

        var versions = await manualChapterVersionRepo.GetByChapterAsync(chapterId);

        var vm = new ManualChapterHistoryViewModel
        {
            ChapterId    = chapterId,
            ProjectId    = projectId,
            ChapterTitle = chapter.Title,
            Versions     = versions.OrderByDescending(v => v.VersionNumber).Select(v => new ManualChapterVersionRowViewModel
            {
                VersionId      = v.Id,
                VersionNumber  = v.VersionNumber,
                SnapshotReason = v.SnapshotReason.ToString(),
                CreatedAt      = v.SnapshotAt,
                ContentLength  = v.RawContent?.Length ?? 0
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearManualChapterHistory(Guid chapterId, Guid projectId)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        try
        {
            await manualUploadService.ClearVersionHistoryAsync(chapterId, author.Id);
            TempData["Success"] = "Version history cleared.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("ManualChapters", new { projectId });
    }
}
