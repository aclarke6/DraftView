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
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    IPublicationService publicationService,
    IUserService userService,
    IDashboardService dashboardService,
    ISyncService syncService,
    IUserRepository userRepo,
    IScrivenerProjectDiscoveryService discoveryService,
    IInvitationRepository invitationRepo,
    IServiceScopeFactory scopeFactory,
    ISyncProgressTracker progressTracker,
    IReaderAccessRepository readerAccessRepo,
    ILogger<AuthorController> logger) : BaseController(userRepo)
{
    // ---------------------------------------------------------------------------
    // Dashboard
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var author = await GetAuthorAsync();
        if (author is null)
            return RedirectToAction("Index", "Reader");

        var projects          = await projectRepo.GetAllAsync();
        var active            = await projectRepo.GetReaderActiveProjectAsync();
        var publishedChapters = active is not null
            ? await publicationService.GetPublishedChaptersAsync(active.Id) : [];
        var failures      = await dashboardService.GetEmailHealthSummaryAsync();
        var readers       = await userRepo.GetAllBetaReadersAsync();
        var notifications = await dashboardService.GetRecentNotificationsAsync(author.Id, maxItems: 20);

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
    // Sync
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(Guid projectId)
    {
        var guard = await RequireAuthorAsync();
        if (guard is not null) return guard;

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
            var bgProjectRepo = scope.ServiceProvider.GetRequiredService<IScrivenerProjectRepository>();
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
        var guard = await RequireAuthorAsync();
        if (guard is not null) return guard;

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
        var guard = await RequireAuthorAsync();
        if (guard is not null) return guard;

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
        var guard = await RequireAuthorAsync();
        if (guard is not null) return guard;

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
        var guard = await RequireAuthorAsync();
        if (guard is not null) return guard;

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

        ViewBag.Project     = project;
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
        return Redirect(Url.Action("Sections", new
        {
            projectId
        }) + "#section-" + chapterId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishChapter(Guid chapterId, Guid projectId)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        await publicationService.UnpublishChapterAsync(chapterId, author.Id);
        TempData["Success"] = "Chapter unpublished.";
        return Redirect(Url.Action("Sections", new
        {
            projectId
        }) + "#section-" + chapterId);
    }

    // ---------------------------------------------------------------------------
    // Readers
    // ---------------------------------------------------------------------------
    public async Task<IActionResult> Readers()
    {
        var guard = await RequireAuthorAsync();
        if (guard is not null) return guard;

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
                DisplayName = r.DisplayName == "Pending" ? "-" : r.DisplayName,
                Email       = r.Email,
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

        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

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
            await userService.IssueInvitationAsync(model.Email, policy, expiresAtUtc, author.Id);

            TempData["Success"] = $"Invitation sent to {model.Email}.";
            return RedirectToAction("Readers");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "InviteReader database failure for {Email} by author {AuthorId}", model.Email, author.Id);
            ModelState.AddModelError(string.Empty, "Unable to send invitation. Please check the details and try again.");
            return View(model);
        }
        catch (InvariantViolationException ex)
        {
            logger.LogWarning(ex, "InviteReader validation failure for {Email} by author {AuthorId}", model.Email, author.Id);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InviteReader unexpected failure for {Email} by author {AuthorId}", model.Email, author.Id);
            ModelState.AddModelError(string.Empty, "Unable to send invitation due to an unexpected error.");
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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();
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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();
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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        var discovered = await discoveryService.DiscoverAsync(author.Id);
        return View(discovered);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProjects(List<string> selectedUuids)
    {
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        if (selectedUuids is null || selectedUuids.Count == 0)
        {
            TempData["Error"] = "No projects selected.";
            return RedirectToAction("Projects");
        }

        var discovered = await discoveryService.DiscoverAsync(author.Id);
        var toAdd      = discovered
            .Where(d => selectedUuids.Contains(d.ScrivenerRootUuid) && !d.AlreadyAdded)
            .ToList();

        var addedCount = 0;
        foreach (var d in toAdd)
        {
            try
            {
                var softDeleted = await projectRepo.GetSoftDeletedByScrivenerRootUuidAsync(d.ScrivenerRootUuid);
                if (softDeleted is not null)
                {
                    softDeleted.Restore(d.Name);
                    addedCount++;
                }
                else
                {
                    var project = ScrivenerProject.Create(d.Name, d.DropboxPath, author.Id, d.ScrivenerRootUuid);
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
            var project  = projects.FirstOrDefault(p => p.ScrivenerRootUuid == d.ScrivenerRootUuid);
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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

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
            Email               = reader.Email,
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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

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
        var author = await GetAuthorAsync();
        if (author is null) return Forbid();

        // Revoke all ReaderAccess for this author
        var allProjects = await projectRepo.GetAllAsync();
        foreach (var project in allProjects.Where(p => !p.IsSoftDeleted))
        {
            var access = await readerAccessRepo.GetByReaderAndProjectAsync(userId, project.Id);
            access?.Revoke();
        }

        // Deactivate the user
        try { await userService.DeactivateUserAsync(userId, author.Id); }
        catch { /* already inactive */ }

        await GetUnitOfWork().SaveChangesAsync();
        TempData["Success"] = "Reader removed.";
        return RedirectToAction("Readers");
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------
    private async Task<User?> GetAuthorAsync()
    {
        var email = User.Identity?.Name;
        if (email is null) return null;
        var user = await userRepo.GetByEmailAsync(email);
        return user?.Role == Role.Author ? user : null;
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



















