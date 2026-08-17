using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.RegularExpressions;
using DraftView.Domain.Diff;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;

namespace DraftView.Web.Controllers;

#pragma warning disable CS9107
public class ReaderController(
    IProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ICommentService commentService,
    IReadingProgressService progressService,
    IUserRepository userRepository,
    IUserPreferencesRepository userPreferencesRepo,
    IReaderAccessRepository readerAccessRepo,
    ISectionVersionRepository sectionVersionRepo,
    IReadEventRepository readEventRepo,
    ISectionDiffService sectionDiffService,
    IHumanOverrideService humanOverrideService,
    IPassageAnchorService passageAnchorService,
    IAccessRequestRepository accessRequestRepo,
    IReaderDashboardService readerDashboardService,
    ILogger<ReaderController> logger)
    : BaseReaderController(projectRepo, sectionRepo, commentService, progressService,
                           userRepository, readerAccessRepo, humanOverrideService, passageAnchorService, logger)
{
    private readonly IUserPreferencesRepository _userPreferencesRepo = userPreferencesRepo;
    private readonly IPassageAnchorService _passageAnchorService = passageAnchorService;
    private readonly IReaderDashboardService _readerDashboardService = readerDashboardService;
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    // -----------------------------------------------------------------------
    // GET: /Reader/Dashboard
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        if (IsMobile())
            return await MobileDashboard(user);

        return await DesktopDashboard(user);
    }

    public IActionResult Index() => RedirectToAction("Dashboard");

    // -----------------------------------------------------------------------
    // GET: /Reader/Chapters?projectId=...  (mobile entry point)
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Chapters(Guid projectId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var project = await ProjectRepo.GetByIdAsync(projectId);
        if (project is null || !project.IsReaderActive || project.IsSoftDeleted)
            return View("NoActiveProject");

        var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);

        var folderChildIds = allSections
            .Where(s => s.NodeType == NodeType.Folder && s.ParentId.HasValue)
            .Select(s => s.ParentId!.Value)
            .ToHashSet();

        var sortOrderById = allSections.ToDictionary(s => s.Id, s => s.SortOrder);

        var publishedChapters = allSections
            .Where(s => s.NodeType == NodeType.Folder && s.IsPublished && !s.IsSoftDeleted
                        && !folderChildIds.Contains(s.Id))
            .OrderBy(s => s.ParentId.HasValue ? sortOrderById.GetValueOrDefault(s.ParentId.Value) : 0)
            .ThenBy(s => s.SortOrder)
            .ToList();

        var chapterRows = new List<MobileChapterRowViewModel>();
        foreach (var chapter in publishedChapters)
        {
            var hasRead    = await ProgressService.HasReadSectionAsync(user.Id, chapter.Id);
            var sceneCount = allSections.Count(s => s.ParentId == chapter.Id
                                                    && s.NodeType == NodeType.Document
                                                    && s.IsPublished && !s.IsSoftDeleted);
            chapterRows.Add(new MobileChapterRowViewModel {
                Chapter    = chapter,
                HasRead    = hasRead,
                SceneCount = sceneCount
            });
        }

        Guid? lastReadSceneId   = null;
        Guid? lastReadChapterId = null;

        var lastReadEvent = await ProgressService.GetLastReadEventAsync(user.Id, project.Id);
        if (lastReadEvent is not null)
        {
            var lastSection = allSections.FirstOrDefault(s => s.Id == lastReadEvent.SectionId);
            if (lastSection?.NodeType == NodeType.Document)
            {
                lastReadSceneId   = lastSection.Id;
                lastReadChapterId = lastSection.ParentId;
            }
        }

        return View("MobileChapters", new MobileChaptersViewModel {
            ProjectName       = project.Name,
            ProjectId         = project.Id,
            Chapters          = chapterRows,
            LastReadSceneId   = lastReadSceneId,
            LastReadChapterId = lastReadChapterId
        });
    }

    // -----------------------------------------------------------------------
    // GET: /Reader/Scenes?chapterId=...  (mobile)
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Scenes(Guid chapterId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var chapter = await SectionRepo.GetByIdAsync(chapterId);
        if (chapter is null || !chapter.IsPublished)
            return NotFound();

        var project = await ProjectRepo.GetByIdAsync(chapter.ProjectId);
        if (project is null)
            return NotFound();

        var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);

        var scenes = allSections
            .Where(s => s.ParentId == chapter.Id &&
                        s.NodeType == NodeType.Document &&
                        s.IsPublished && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var sceneRows = new List<MobileSceneRowViewModel>();
        foreach (var scene in scenes)
        {
            var hasRead = await ProgressService.HasReadSectionAsync(user.Id, scene.Id);
            sceneRows.Add(new MobileSceneRowViewModel { Scene = scene, HasRead = hasRead });
        }

        return View("MobileScenes", new MobileScenesViewModel {
            ProjectName = project.Name,
            ProjectId   = project.Id,
            Chapter     = chapter,
            Scenes      = sceneRows
        });
    }

    // -----------------------------------------------------------------------
    // GET: /Reader/ChapterComments/{chapterId}  (mobile)
    // -----------------------------------------------------------------------
    public async Task<IActionResult> ChapterComments(Guid chapterId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var chapter = await SectionRepo.GetByIdAsync(chapterId);
        if (chapter is null || !chapter.IsPublished || chapter.NodeType != NodeType.Folder)
            return NotFound();

        var project = await ProjectRepo.GetByIdAsync(chapter.ProjectId);
        if (project is null)
            return NotFound();

        var isModerator = user.Role == Role.Author;

        var commentsRaw = await CommentService.GetThreadsForSectionAsync(chapterId, user.Id);
        var comments    = await BuildCommentDisplayModelsAsync(commentsRaw, user.Id, project.AuthorId, isModerator);

        return View("MobileChapterComments", new MobileChapterCommentsViewModel {
            Chapter                = chapter,
            ProjectName            = project.Name,
            ProjectId              = project.Id,
            Comments               = comments,
            CurrentUserId          = user.Id,
            CurrentUserIsModerator = isModerator
        });
    }

    // -----------------------------------------------------------------------
    // GET: /Reader/SceneComments/{sceneId}  (mobile)
    // -----------------------------------------------------------------------
    public async Task<IActionResult> SceneComments(Guid sceneId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var scene = await SectionRepo.GetByIdAsync(sceneId);
        if (scene is null || !scene.IsPublished || scene.NodeType != NodeType.Document)
            return NotFound();

        var chapter = scene.ParentId.HasValue
            ? await SectionRepo.GetByIdAsync(scene.ParentId.Value)
            : null;
        if (chapter is null)
            return NotFound();

        var project = await ProjectRepo.GetByIdAsync(scene.ProjectId);
        if (project is null)
            return NotFound();

        var isModerator = user.Role == Role.Author;

        var commentsRaw = await CommentService.GetThreadsForSectionAsync(sceneId, user.Id);
        var comments    = await BuildCommentDisplayModelsAsync(commentsRaw, user.Id, project.AuthorId, isModerator);

        return View("MobileSceneComments", new MobileSceneCommentsViewModel {
            Scene                  = scene,
            Chapter                = chapter,
            ProjectName            = project.Name,
            ProjectId              = project.Id,
            Comments               = comments,
            CurrentUserId          = user.Id,
            CurrentUserIsModerator = isModerator
        });
    }

    // -----------------------------------------------------------------------
    // GET: /Reader/Browse/{id}  (desktop)
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Browse(Guid id)
    {
        var project = await ProjectRepo.GetReaderActiveProjectAsync();
        if (project is null)
            return View("NoActiveProject");

        var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);
        var topSection  = allSections.FirstOrDefault(s => s.Id == id);
        if (topSection is null)
            return NotFound();

        // Walk up to the book root so Browse shows all acts, not just one act's chapters.
        var browseRoot = topSection.ParentId.HasValue
            ? allSections.FirstOrDefault(s => s.Id == topSection.ParentId.Value) ?? topSection
            : topSection;

        return View("DesktopBrowse", new DesktopSectionContentsViewModel {
            TopLevelSection = browseRoot,
            Groups          = BuildContentGroups(browseRoot, allSections),
            ProjectName     = project.Name
        });
    }

    // -----------------------------------------------------------------------
    // GET: /Reader/Read/{id}
    // Routes to desktop chapter view or mobile scene view based on User-Agent
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Read(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        if (IsMobile())
            return await MobileRead(id, user);

        return await DesktopRead(id, user);
    }

    // -----------------------------------------------------------------------
    // POST: /Reader/DismissBanner
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissBanner(Guid sectionId, int versionNumber)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        await ProgressService.DismissBannerAsync(sectionId, user.Id, versionNumber);
        return Ok();
    }

    /// <summary>
    /// Captures the reader's latest resume position without changing the active resume behavior.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureResumePosition([FromBody] CaptureResumePositionRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        try
        {
            await ProgressService.CaptureResumePositionAsync(request, user.Id);
            return Ok();
        }
        catch (UnauthorisedOperationException)
        {
            return Forbid();
        }
        catch (InvariantViolationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // POST: /Reader/CapturePassageAnchorSelection
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapturePassageAnchorSelection(
        [FromBody] CreatePassageAnchorRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        try
        {
            await _passageAnchorService.ValidateSelectionAsync(request, user.Id);
            return Ok();
        }
        catch (UnauthorisedOperationException)
        {
            return Forbid();
        }
        catch (InvariantViolationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // -----------------------------------------------------------------------
    // Private: Desktop implementations
    // -----------------------------------------------------------------------
    private async Task<IActionResult> DesktopDashboard(Domain.Entities.User user)
    {
        var projectIds = user.Role == Role.Author
            ? (await ProjectRepo.GetAllAsync())
                .Where(p => p.IsReaderActive && !p.IsSoftDeleted)
                .Select(p => p.Id)
                .ToList()
            : (await ReaderAccessRepo.GetByReaderIdAsync(user.Id))
                .Select(a => a.ProjectId)
                .ToList();

        var visibleRequests = await accessRequestRepo.GetVisibleByReaderIdAsync(user.Id, DateTime.UtcNow.Date);
        var requestRows = new List<ReaderDashboardRequestViewModel>();
        foreach (var req in visibleRequests)
        {
            var reqProject = await ProjectRepo.GetByIdAsync(req.ProjectId);
            requestRows.Add(new ReaderDashboardRequestViewModel
            {
                Request     = req,
                ProjectName = reqProject?.Name ?? "Unknown book"
            });
        }

        var viewModel = new DesktopDashboardViewModel { AccessRequests = requestRows };

        foreach (var projectId in projectIds)
        {
            var project = await ProjectRepo.GetByIdAsync(projectId);
            if (project is null || !project.IsReaderActive || project.IsSoftDeleted)
                continue;

            var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);
            var folderChildIds = allSections
                .Where(s => s.NodeType == NodeType.Folder && s.ParentId.HasValue)
                .Select(s => s.ParentId!.Value)
                .ToHashSet();

            var sortOrderById = allSections.ToDictionary(s => s.Id, s => s.SortOrder);
            var publishedChapters = allSections
                .Where(s => s.NodeType == NodeType.Folder && s.IsPublished && !s.IsSoftDeleted
                            && !folderChildIds.Contains(s.Id))
                .OrderBy(s => s.ParentId.HasValue ? sortOrderById.GetValueOrDefault(s.ParentId.Value) : 0)
                .ThenBy(s => s.SortOrder)
                .ToList();

            var chaptersWithProgress = new List<DesktopChapterProgressViewModel>();
            foreach (var chapter in publishedChapters)
            {
                var hasRead = await ProgressService.HasReadSectionAsync(user.Id, chapter.Id);
                chaptersWithProgress.Add(new DesktopChapterProgressViewModel {
                    Chapter = chapter,
                    HasRead = hasRead
                });
            }

            viewModel.Projects.Add(new DesktopProjectViewModel {
                ProjectId         = project.Id,
                ProjectName       = project.Name,
                TotalChapters     = publishedChapters.Count,
                ReadChapters      = chaptersWithProgress.Count(c => c.HasRead),
                PublishedChapters = chaptersWithProgress
            });
        }

        // Populate comment counts via application service
        var allChapterIds = viewModel.Projects
            .SelectMany(p => p.PublishedChapters.Select(c => c.Chapter.Id))
            .ToList();
        var commentCounts = await _readerDashboardService.GetReaderChapterCommentCountsAsync(user.Id, allChapterIds);
        foreach (var proj in viewModel.Projects)
            foreach (var ch in proj.PublishedChapters)
                if (commentCounts.TryGetValue(ch.Chapter.Id, out var count))
                    ch.ReaderCommentCount = count;

        // Resolve resume target via application service
        var resumeTarget = await _readerDashboardService.GetCrossProjectResumeTargetAsync(user.Id, projectIds);
        if (resumeTarget is not null)
        {
            viewModel.ContinueReadingUrl = resumeTarget.SceneId.HasValue
                ? Url.Action("Read", new { id = resumeTarget.ChapterId }) + "#scene-" + resumeTarget.SceneId
                : Url.Action("Read", new { id = resumeTarget.ChapterId });
        }

        return View("DesktopDashboard", viewModel);
    }

    private async Task<IActionResult> MobileDashboard(Domain.Entities.User user)
    {
        var projectIds = user.Role == Role.Author
            ? (await ProjectRepo.GetAllAsync())
                .Where(p => p.IsReaderActive && !p.IsSoftDeleted)
                .Select(p => p.Id)
                .ToList()
            : (await ReaderAccessRepo.GetByReaderIdAsync(user.Id))
                .Select(a => a.ProjectId)
                .ToList();

        var projectId = projectIds.FirstOrDefault();
        if (projectId == Guid.Empty)
            return View("NoActiveProject");

        var project = await ProjectRepo.GetByIdAsync(projectId);
        if (project is null || !project.IsReaderActive || project.IsSoftDeleted)
            return View("NoActiveProject");

        return RedirectToAction("Chapters", new { projectId = project.Id });
    }

    private async Task<IActionResult> DesktopRead(Guid id, Domain.Entities.User user)
    {
        var chapter = await SectionRepo.GetByIdAsync(id);
        if (chapter is null || !chapter.IsPublished)
            return NotFound();

        // Scene (Document) URL — redirect to its parent chapter so the desktop layout loads correctly
        if (chapter.NodeType == NodeType.Document && chapter.ParentId.HasValue)
            return RedirectToAction("Read", new { id = chapter.ParentId.Value });

        var isModerator = user.Role == Role.Author;

        await ProgressService.RecordOpenAsync(id, user.Id);

        var project     = await ProjectRepo.GetByIdAsync(chapter.ProjectId);
        var allSections = project is not null
            ? await SectionRepo.GetByProjectIdAsync(project.Id)
            : new List<Section>();

        var scenes = allSections
            .Where(s => s.ParentId == chapter.Id &&
                        s.NodeType == NodeType.Document &&
                        s.IsPublished && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var scenesWithComments = new List<SceneWithComments>();
        foreach (var scene in scenes)
        {
            var sceneWithComments = await BuildSceneWithCommentsAsync(
                scene,
                user,
                project?.AuthorId ?? Guid.Empty,
                isModerator);
            scenesWithComments.Add(sceneWithComments);
        }

        var chapterCommentsRaw = await CommentService.GetThreadsForSectionAsync(id, user.Id);
        var chapterComments    = await BuildCommentDisplayModelsAsync(chapterCommentsRaw, user.Id, project?.AuthorId ?? Guid.Empty, isModerator);
        var breadcrumb         = BuildBreadcrumb(chapter, allSections);
        var topAncestor        = GetTopLevelAncestor(chapter, allSections);
        var preferences        = await _userPreferencesRepo.GetByUserIdAsync(user.Id);

        DesktopSectionContentsViewModel? bookContents = null;
        if (topAncestor is not null)
        {
            // topAncestor is the act/part (one level below the book root).
            // Walk up one more level so BuildContentGroups covers all sibling acts.
            var bookRoot = topAncestor.ParentId.HasValue
                ? allSections.FirstOrDefault(s => s.Id == topAncestor.ParentId.Value) ?? topAncestor
                : topAncestor;

            bookContents = new DesktopSectionContentsViewModel {
                TopLevelSection = topAncestor,
                Groups          = BuildContentGroups(bookRoot, allSections),
                ProjectName     = project?.Name ?? string.Empty
            };
        }

        return View("DesktopRead", new DesktopChapterReadViewModel {
            Chapter                = chapter,
            Breadcrumb             = breadcrumb,
            Scenes                 = scenesWithComments,
            ChapterComments        = chapterComments,
            BookContents           = bookContents,
            ProjectName            = project?.Name ?? string.Empty,
            CurrentUserId          = user.Id,
            CurrentUserIsModerator = isModerator,
            ProseFont              = preferences?.ProseFont ?? ProseFont.SystemSerif,
            ProseFontSize          = preferences?.ProseFontSize ?? ProseFontSize.Medium
        });
    }

    private async Task<IActionResult> MobileRead(Guid id, Domain.Entities.User user)
    {
        var scene = await SectionRepo.GetByIdAsync(id);
        if (scene is null || !scene.IsPublished || scene.NodeType != NodeType.Document)
            return NotFound();

        var chapter = scene.ParentId.HasValue
            ? await SectionRepo.GetByIdAsync(scene.ParentId.Value)
            : null;
        if (chapter is null)
            return NotFound();

        var project = await ProjectRepo.GetByIdAsync(scene.ProjectId);
        if (project is null)
            return NotFound();

        var isModerator = user.Role == Role.Author;

        await ProgressService.RecordOpenAsync(id, user.Id);

        var (resolvedHtml, currentSectionVersionId, currentVersionNumber, resumeCaptureText, resumeRestoreTarget, diffParagraphs, updatedSinceLastRead, showUpdateBanner) =
            await ResolveSceneContentAndDiffAsync(scene, user.Id);

        var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);
        var (prevSceneId, nextSceneId) = GetPrevNextSceneIds(scene.Id, chapter.Id, allSections);

        var commentsRaw       = await CommentService.GetThreadsForSectionAsync(id, user.Id);
        var sceneCommentCount = commentsRaw.Count(c => !c.IsSoftDeleted);
        var preferences       = await _userPreferencesRepo.GetByUserIdAsync(user.Id);

        return View("MobileRead", new MobileReadViewModel {
            Scene                    = scene,
            Chapter                  = chapter,
            ProjectName              = project.Name,
            SceneCommentCount        = sceneCommentCount,
            PrevSceneId              = prevSceneId,
            NextSceneId              = nextSceneId,
            ProseFont                = preferences?.ProseFont ?? ProseFont.SystemSerif,
            ProseFontSize            = preferences?.ProseFontSize ?? ProseFontSize.Medium,
            ResolvedHtmlContent      = resolvedHtml,
            CurrentSectionVersionId  = currentSectionVersionId,
            ResumeCaptureText        = resumeCaptureText,
            HasResumeRestoreTarget   = resumeRestoreTarget?.HasTarget ?? false,
            ResumeRestoreStartOffset = resumeRestoreTarget?.StartOffset,
            ResumeRestoreEndOffset   = resumeRestoreTarget?.EndOffset,
            ResumeRestoreStatus      = resumeRestoreTarget?.Status,
            ResumeRestoreConfidenceScore = resumeRestoreTarget?.ConfidenceScore,
            ResumeRestoreMatchMethod = resumeRestoreTarget?.MatchMethod,
            CurrentVersionNumber     = currentVersionNumber,
            DiffParagraphs           = diffParagraphs,
            UpdatedSinceLastRead     = updatedSinceLastRead,
            ShowUpdateBanner         = showUpdateBanner
        });
    }

    /// <summary>
    /// Builds a SceneWithComments view model by resolving content, computing diff,
    /// and loading comments for a scene.
    /// </summary>
    private async Task<SceneWithComments> BuildSceneWithCommentsAsync(
        Section scene,
        Domain.Entities.User user,
        Guid projectAuthorId,
        bool isModerator,
        CancellationToken ct = default)
    {
        await ProgressService.RecordOpenAsync(scene.Id, user.Id, ct);

        var (resolvedHtml, currentSectionVersionId, currentVersionNumber, resumeCaptureText, resumeRestoreTarget, diffParagraphs, updatedSinceLastRead, showUpdateBanner) =
            await ResolveSceneContentAndDiffAsync(scene, user.Id, ct);

        var comments = await CommentService.GetThreadsForSectionAsync(scene.Id, user.Id, ct);
        var displayComments = await BuildCommentDisplayModelsAsync(comments, user.Id, projectAuthorId, isModerator);

        return new SceneWithComments
        {
            Scene = scene,
            Comments = displayComments,
            ResolvedHtmlContent = resolvedHtml,
            CurrentSectionVersionId = currentSectionVersionId,
            ResumeCaptureText = resumeCaptureText,
            HasResumeRestoreTarget = resumeRestoreTarget?.HasTarget ?? false,
            ResumeRestoreStartOffset = resumeRestoreTarget?.StartOffset,
            ResumeRestoreEndOffset = resumeRestoreTarget?.EndOffset,
            ResumeRestoreStatus = resumeRestoreTarget?.Status,
            ResumeRestoreConfidenceScore = resumeRestoreTarget?.ConfidenceScore,
            ResumeRestoreMatchMethod = resumeRestoreTarget?.MatchMethod,
            DiffParagraphs = diffParagraphs,
            UpdatedSinceLastRead = updatedSinceLastRead,
            ShowUpdateBanner = showUpdateBanner,
            CurrentVersionNumber = currentVersionNumber
        };
    }

    /// <summary>
    /// Resolves scene content from the latest version (or fallback to working content),
    /// computes diff if reader has a prior read version, and updates reader progress.
    /// Returns: (resolvedHtml, currentVersionNumber, diffParagraphs, updatedSinceLastRead, showUpdateBanner)
    /// </summary>
    private async Task<(string? resolvedHtml, Guid? currentSectionVersionId, int? currentVersionNumber, string resumeCaptureText, ResumeRestoreTargetDto? resumeRestoreTarget, IReadOnlyList<ParagraphDiffResult> diffParagraphs, bool updatedSinceLastRead, bool showUpdateBanner)>
        ResolveSceneContentAndDiffAsync(
            Section scene,
            Guid userId,
            CancellationToken ct = default)
    {
        var latestVersion = await sectionVersionRepo.GetLatestAsync(scene.Id, ct);
        var resolvedHtml = latestVersion?.HtmlContent ?? scene.HtmlContent;
        var currentSectionVersionId = latestVersion?.Id;
        var currentVersionNumber = latestVersion?.VersionNumber;
        var resumeCaptureText = CanonicalizeForCapture(resolvedHtml);
        var resumeRestoreTarget = await ProgressService.GetResumeRestoreTargetAsync(
            scene.Id,
            currentSectionVersionId,
            userId,
            ct);
        var readEvent = await readEventRepo.GetAsync(scene.Id, userId, ct);
        var lastReadVersionNumber = readEvent?.LastReadVersionNumber;

        var diffResult = await sectionDiffService.GetDiffForReaderAsync(
            scene.Id, lastReadVersionNumber, ct);

        var updatedSinceLastRead = diffResult is not null
            && diffResult.HasChanges
            && readEvent?.LastReadVersionNumber is not null
            && currentVersionNumber.HasValue;

        var showUpdateBanner = diffResult is not null
            && diffResult.HasChanges
            && readEvent?.LastReadVersionNumber is not null
            && currentVersionNumber.HasValue
            && readEvent?.BannerDismissedAtVersion != diffResult.CurrentVersionNumber;

        if (latestVersion is not null)
        {
            await ProgressService.UpdateLastReadVersionAsync(scene.Id, userId, latestVersion.VersionNumber, ct);
        }

        var diffParagraphs = latestVersion is null && diffResult?.HasChanges == true
            ? diffResult.Paragraphs
            : Array.Empty<ParagraphDiffResult>();

        return (resolvedHtml, currentSectionVersionId, currentVersionNumber, resumeCaptureText, resumeRestoreTarget, diffParagraphs, updatedSinceLastRead, showUpdateBanner);
    }

    /// <summary>
    /// Converts the reader-visible HTML source into canonical plain text for client-side resume capture hints.
    /// </summary>
    private static string CanonicalizeForCapture(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutTags = HtmlTagRegex.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex.Replace(decoded, " ").Trim();
    }

    /// <summary>
    /// Determines the previous and next scene IDs for mobile navigation.
    /// Returns (prevSceneId, nextSceneId) tuples.
    /// </summary>
    private static (Guid? prevSceneId, Guid? nextSceneId) GetPrevNextSceneIds(
        Guid currentSceneId,
        Guid chapterId,
        IReadOnlyList<Section> allSections)
    {
        var siblingScenes = allSections
            .Where(s => s.ParentId == chapterId &&
                        s.NodeType == NodeType.Document &&
                        s.IsPublished && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var currentIndex = siblingScenes.FindIndex(s => s.Id == currentSceneId);
        var prevSceneId = currentIndex > 0
            ? siblingScenes[currentIndex - 1].Id
            : (Guid?)null;
        var nextSceneId = currentIndex >= 0 && currentIndex < siblingScenes.Count - 1
            ? siblingScenes[currentIndex + 1].Id
            : (Guid?)null;

        return (prevSceneId, nextSceneId);
    }
}
