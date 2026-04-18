using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;
namespace DraftView.Web.Controllers;

#pragma warning disable CS9107, CS9113
[Authorize(Policy = "RequireAuthorPolicy")]
public class AuthorController(
    IProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ISectionVersionRepository sectionVersionRepo,
    IPublicationService publicationService,
    IUserService userService,
    IDashboardService dashboardService,
    ISyncService syncService,
    IUserRepository userRepo,
    IProjectDiscoveryService discoveryService,
    IInvitationRepository invitationRepo,
    IServiceScopeFactory scopeFactory,
    ISyncProgressTracker progressTracker,
    IReaderAccessRepository readerAccessRepo,
    IVersioningService versioningService,
    IHtmlDiffService htmlDiffService,
    IChangeClassificationService changeClassificationService,
    IImportService importService,
    ISectionTreeService sectionTreeService,
    ILogger<AuthorController> logger) : BaseController(userRepo)
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
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is not null)
        {
            project.MarkSyncing();
            await GetUnitOfWork().SaveChangesAsync();
        }

        _ = Task.Run(async () =>
        {
            using var scope   = scopeFactory.CreateScope();
            var bgSyncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
            var bgProjectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var bgUnitOfWork  = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            try
            {
                await bgSyncService.ParseProjectAsync(projectId);
                logger.LogInformation("Background sync completed for project {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background sync failed for project {ProjectId}: {Message}",
                    projectId, ex.Message);
                try
                {
                    var failedProject = await bgProjectRepo.GetByIdAsync(projectId);
                    if (failedProject is not null && failedProject.SyncStatus == SyncStatus.Syncing)
                    {
                        failedProject.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow,
                            ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
                        await bgUnitOfWork.SaveChangesAsync();
                    }
                }
                catch (Exception innerEx)
                {
                    logger.LogError(innerEx, "Failed to update sync error status for {ProjectId}", projectId);
                }
            }
        });

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
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

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
    // Sections list
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Sections(Guid projectId)
    {
        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        var sections = await sectionRepo.GetByProjectIdAsync(projectId);
        var sorted   = SortDepthFirst(sections);

        var publishable = new HashSet<Guid>();
        foreach (var (s, _) in sorted.Where(x => x.Section.NodeType == NodeType.Folder))
        {
            if (await publicationService.CanPublishAsync(s.Id))
                publishable.Add(s.Id);
        }

        var classificationMap = new Dictionary<Guid, ChangeClassification>();
        foreach (var (chapter, _) in sorted.Where(x =>
                     x.Section.NodeType == NodeType.Folder &&
                     x.Section.IsPublished &&
                     x.Section.ContentChangedSincePublish))
        {
            try
            {
                var documents = sorted
                    .Where(x => x.Section.ParentId == chapter.Id &&
                                x.Section.NodeType == NodeType.Document &&
                                !x.Section.IsSoftDeleted)
                    .Select(x => x.Section)
                    .ToList();

                var highestClassification = ChangeClassification.Polish;
                var hasClassifiableVersion = false;

                foreach (var document in documents)
                {
                    var latestVersion = await sectionVersionRepo.GetLatestAsync(document.Id);
                    if (latestVersion is null) continue;

                    hasClassifiableVersion = true;
                    var diff = htmlDiffService.Compute(
                        latestVersion.HtmlContent,
                        document.HtmlContent ?? string.Empty);

                    var classification = changeClassificationService.Classify(diff);
                    if (classification.HasValue && classification.Value > highestClassification)
                        highestClassification = classification.Value;
                }

                if (hasClassifiableVersion)
                    classificationMap[chapter.Id] = highestClassification;
            }
            catch
            {
                // Classification indicator is advisory only; skip failures silently.
            }
        }

        ViewBag.Project     = project;
        ViewBag.Publishable = publishable;
        ViewBag.ClassificationMap = classificationMap;
        return View(sorted);
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
        var readers = await userRepo.GetAllBetaReadersAsync();

        var rows = new List<ReaderRowViewModel>();
        foreach (var r in readers.Where(r => !r.IsSoftDeleted))
        {
            var invitation = await invitationRepo.GetByUserIdAsync(r.Id);
            var isPending  = invitation is not null
                          && invitation.Status == Domain.Enumerations.InvitationStatus.Pending;

            var status = r.IsActive
                ? ReaderStatus.Active
                : isPending
                    ? ReaderStatus.Invited
                    : ReaderStatus.Inactive;

            rows.Add(new ReaderRowViewModel
            {
                Id          = r.Id,
                DisplayName = string.IsNullOrWhiteSpace(r.DisplayName) ? "Pending reader" : r.DisplayName,
                Email       = string.Empty,
                Status      = status,
                ActivatedAt = r.ActivatedAt
            });
        }

        return View(rows.OrderBy(r => r.DisplayName).ToList());
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

        var s = await sectionRepo.GetByIdAsync(id);
        if (s is null) return NotFound();

        // Resolve parent chapter title if this is a scene
        string? chapterTitle = null;
        if (s.ParentId.HasValue)
        {
            var parent = await sectionRepo.GetByIdAsync(s.ParentId.Value);
            chapterTitle = parent?.Title;
        }

        var comments = await GetCommentService().GetThreadsForSectionAsync(id, author.Id);
        var events   = await GetReadEventRepo().GetBySectionIdAsync(id);

        var nameMap = new Dictionary<Guid, string>();
        foreach (var uid in comments.Select(c => c.AuthorId).Distinct())
        {
            var u = await userRepo.GetByIdAsync(uid);
            nameMap[uid] = u?.DisplayName ?? "Unknown";
        }

        return View(new SectionViewModel
        {
            Section            = s,
            ChapterTitle       = chapterTitle,
            Comments           = comments,
            ReadCount          = events.Count,
            CommentAuthorNames = nameMap
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

        var project = await projectRepo.GetByIdAsync(projectId);
        if (project is null) return NotFound();

        project.SoftDelete();
        await GetUnitOfWork().SaveChangesAsync();

        TempData["Success"] = $"{project.Name} removed. You can re-add it from Add Project.";
        return RedirectToAction("Dashboard");
    }

    // ---------------------------------------------------------------------------
    // Projects discovery
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Projects()
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        var discovered = await discoveryService.DiscoverAsync(author.Id);
        return View(discovered);
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

        var discovered = await discoveryService.DiscoverAsync(author.Id);
        var toAdd      = discovered
            .Where(d => selectedUuids.Contains(d.SyncRootId) && !d.AlreadyAdded)
            .ToList();

        var addedCount = 0;
        foreach (var d in toAdd)
        {
            try
            {
                var softDeleted = await projectRepo.GetSoftDeletedBySyncRootIdAsync(d.SyncRootId);
                if (softDeleted is not null)
                {
                    softDeleted.Restore(d.Name);
                    addedCount++;
                }
                else
                {
                    var project = Project.Create(d.Name, d.DropboxPath, author.Id, d.SyncRootId);
                    await projectRepo.AddAsync(project);
                    addedCount++;
                }
            }
            catch (DuplicateProjectException) { }
        }

        await GetUnitOfWork().SaveChangesAsync();

        foreach (var d in toAdd)
        {
            var projects = await projectRepo.GetAllAsync();
            var project  = projects.FirstOrDefault(p => p.SyncRootId == d.SyncRootId);
            if (project is null) continue;

            try { await syncService.ParseProjectAsync(project.Id); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Initial sync failed for {Name}", d.Name);
            }
        }

        TempData["Success"] = addedCount == 1
            ? $"{toAdd.First().Name} added successfully."
            : $"{addedCount} projects added successfully.";

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

        var reader = await userRepo.GetByIdAsync(readerId);
        if (reader is null) return NotFound();

        var allProjects = await projectRepo.GetAllAsync();
        var activeProjects = allProjects.Where(p => !p.IsSoftDeleted).ToList();

        var accessRecords = await readerAccessRepo.GetByReaderIdAsync(readerId);
        var accessProjectIds = accessRecords.Select(a => a.ProjectId).ToHashSet();

        var invitation = await invitationRepo.GetByUserIdAsync(readerId);
        var isPending = invitation is not null
                     && invitation.Status == Domain.Enumerations.InvitationStatus.Pending;
        var status = reader.IsActive
            ? ReaderStatus.Active
            : isPending ? ReaderStatus.Invited : ReaderStatus.Inactive;

        return View(new ReaderAccessViewModel
        {
            ReaderId            = reader.Id,
            DisplayName         = reader.DisplayName,
            Email               = string.Empty,
            Status              = status,
            ProjectsWithAccess = [.. activeProjects.Where(p => accessProjectIds.Contains(p.Id))],
            ProjectsWithoutAccess = [.. activeProjects.Where(p => !accessProjectIds.Contains(p.Id))]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReaderAccess(
        Guid readerId, List<Guid> grantIds, List<Guid> revokeIds)
    {
        var (author, error) = await RequireCurrentAuthorAsync();
        if (error is not null || author is null) return error ?? Forbid();

        foreach (var projectId in grantIds)
        {
            var existing = await readerAccessRepo.GetByReaderAndProjectAsync(readerId, projectId);
            if (existing is null)
            {
                var access = ReaderAccess.Grant(readerId, author.Id, projectId);
                await readerAccessRepo.AddAsync(access);
            }
            else if (!existing.IsActive)
            {
                existing.Reinstate();
            }
        }

        foreach (var projectId in revokeIds)
        {
            var existing = await readerAccessRepo.GetByReaderAndProjectAsync(readerId, projectId);
            existing?.Revoke();
        }

        await GetUnitOfWork().SaveChangesAsync();
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

        // Revoke all ReaderAccess for this author
        var allProjects = await projectRepo.GetAllAsync();
        foreach (var project in allProjects.Where(p => !p.IsSoftDeleted))
        {
            var access = await readerAccessRepo.GetByReaderAndProjectAsync(userId, project.Id);
            access?.Revoke();
        }

        await userService.SoftDeleteUserAsync(userId, author.Id);

        await GetUnitOfWork().SaveChangesAsync();
        TempData["Success"] = "Reader removed.";
        return RedirectToAction("Readers");
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------
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
            if (!lookup.ContainsKey(key)) lookup[key] = [];
            lookup[key].Add(s);
        }

        foreach (var key in lookup.Keys.ToList())
            lookup[key] = [.. lookup[key].OrderBy(s => s.SortOrder)];

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
