using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
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
    ILogger<ReaderController> logger)
    : BaseReaderController(projectRepo, sectionRepo, commentService, progressService,
                           userRepository, readerAccessRepo, logger)
{
    private readonly IUserPreferencesRepository _userPreferencesRepo = userPreferencesRepo;

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

        return View("DesktopBrowse", new DesktopSectionContentsViewModel {
            TopLevelSection = topSection,
            Groups          = BuildContentGroups(topSection, allSections),
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

        // Kindle-style resume — redirect to last read position across all projects
        ReadEvent? resume = null;
        foreach (var pid in projectIds)
        {
            var ev = await ProgressService.GetLastReadEventAsync(user.Id, pid);
            if (ev is not null && (resume is null || ev.LastOpenedAt > resume.LastOpenedAt))
                resume = ev;
        }
        if (resume is not null)
        {
            var resumeSection = await SectionRepo.GetByIdAsync(resume.SectionId);
            if (resumeSection is not null && resumeSection.IsPublished)
            {
                // Scene (Document) — redirect to parent chapter
                if (resumeSection.NodeType == NodeType.Document && resumeSection.ParentId.HasValue)
                {
                    var parentChapter = await SectionRepo.GetByIdAsync(resumeSection.ParentId.Value);
                    if (parentChapter is not null && parentChapter.IsPublished)
                        return Redirect(Url.Action("Read", new { id = parentChapter.Id }) + "#scene-" + resumeSection.Id);
                }
                // Chapter (Folder) — redirect directly
                else if (resumeSection.NodeType == NodeType.Folder)
                {
                    return RedirectToAction("Read", new { id = resumeSection.Id });
                }
            }
        }

        var viewModel = new DesktopDashboardViewModel();

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

        // Kindle-style resume — redirect to last read position across all projects
        ReadEvent? resume = null;
        foreach (var pid in projectIds)
        {
            var ev = await ProgressService.GetLastReadEventAsync(user.Id, pid);
            if (ev is not null && (resume is null || ev.LastOpenedAt > resume.LastOpenedAt))
                resume = ev;
        }
        if (resume is not null)
        {
            var resumeSection = await SectionRepo.GetByIdAsync(resume.SectionId);
            if (resumeSection is not null && resumeSection.IsPublished)
            {
                // Scene (Document) â€” mobile reads the scene directly
                if (resumeSection.NodeType == NodeType.Document)
                {
                    return RedirectToAction("Read", new
                    {
                        id = resumeSection.Id
                    });
                }

                // Chapter (Folder) â€” mobile should go to the scene list, not Read(chapterId)
                if (resumeSection.NodeType == NodeType.Folder)
                {
                    return RedirectToAction("Scenes", new
                    {
                        chapterId = resumeSection.Id
                    });
                }
            }
        }

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
            var sceneWithComments = await BuildSceneWithCommentsAsync(scene, user, isModerator);
            scenesWithComments.Add(sceneWithComments);
        }

        var chapterCommentsRaw = await CommentService.GetThreadsForSectionAsync(id, user.Id);
        var chapterComments    = await BuildCommentDisplayModelsAsync(chapterCommentsRaw, user.Id, isModerator);
        var breadcrumb         = BuildBreadcrumb(chapter, allSections);
        var topAncestor        = GetTopLevelAncestor(chapter, allSections);
        var preferences        = await _userPreferencesRepo.GetByUserIdAsync(user.Id);

        DesktopSectionContentsViewModel? bookContents = null;
        if (topAncestor is not null)
        {
            bookContents = new DesktopSectionContentsViewModel {
                TopLevelSection = topAncestor,
                Groups          = BuildContentGroups(topAncestor, allSections),
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

        var (resolvedHtml, currentVersionNumber, diffParagraphs) = 
            await ResolveSceneContentAndDiffAsync(scene, user.Id);

        var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);
        var (prevSceneId, nextSceneId) = GetPrevNextSceneIds(scene.Id, chapter.Id, allSections);

        var commentsRaw = await CommentService.GetThreadsForSectionAsync(id, user.Id);
        var comments    = await BuildCommentDisplayModelsAsync(commentsRaw, user.Id, isModerator);
        var preferences = await _userPreferencesRepo.GetByUserIdAsync(user.Id);

        return View("MobileRead", new MobileReadViewModel {
            Scene                  = scene,
            Chapter                = chapter,
            ProjectName            = project.Name,
            Comments               = comments,
            PrevSceneId            = prevSceneId,
            NextSceneId            = nextSceneId,
            CurrentUserId          = user.Id,
            CurrentUserIsModerator = isModerator,
            ProseFont              = preferences?.ProseFont ?? ProseFont.SystemSerif,
            ProseFontSize          = preferences?.ProseFontSize ?? ProseFontSize.Medium,
            ResolvedHtmlContent    = resolvedHtml,
            CurrentVersionNumber   = currentVersionNumber,
            DiffParagraphs         = diffParagraphs
        });
    }

    /// <summary>
    /// Builds a SceneWithComments view model by resolving content, computing diff,
    /// and loading comments for a scene.
    /// </summary>
    private async Task<SceneWithComments> BuildSceneWithCommentsAsync(
        Section scene,
        Domain.Entities.User user,
        bool isModerator,
        CancellationToken ct = default)
    {
        await ProgressService.RecordOpenAsync(scene.Id, user.Id, ct);

        var (resolvedHtml, _, diffParagraphs) = 
            await ResolveSceneContentAndDiffAsync(scene, user.Id, ct);

        var comments = await CommentService.GetThreadsForSectionAsync(scene.Id, user.Id, ct);
        var displayComments = await BuildCommentDisplayModelsAsync(comments, user.Id, isModerator);

        return new SceneWithComments
        {
            Scene = scene,
            Comments = displayComments,
            ResolvedHtmlContent = resolvedHtml,
            DiffParagraphs = diffParagraphs
        };
    }

    /// <summary>
    /// Resolves scene content from the latest version (or fallback to working content),
    /// computes diff if reader has a prior read version, and updates reader progress.
    /// Returns: (resolvedHtml, currentVersionNumber, diffParagraphs)
    /// </summary>
    private async Task<(string? resolvedHtml, int? currentVersionNumber, IReadOnlyList<ParagraphDiffResult> diffParagraphs)> 
        ResolveSceneContentAndDiffAsync(
            Section scene,
            Guid userId,
            CancellationToken ct = default)
    {
        var latestVersion = await sectionVersionRepo.GetLatestAsync(scene.Id, ct);
        var resolvedHtml = latestVersion?.HtmlContent ?? scene.HtmlContent;
        var currentVersionNumber = latestVersion?.VersionNumber;

        var readEvent = await readEventRepo.GetAsync(scene.Id, userId, ct);
        var lastReadVersionNumber = readEvent?.LastReadVersionNumber;

        var diffResult = await sectionDiffService.GetDiffForReaderAsync(
            scene.Id, lastReadVersionNumber, ct);

        if (latestVersion is not null)
        {
            await ProgressService.UpdateLastReadVersionAsync(scene.Id, userId, latestVersion.VersionNumber, ct);
        }

        var diffParagraphs = diffResult?.HasChanges == true
            ? diffResult.Paragraphs
            : Array.Empty<ParagraphDiffResult>();

        return (resolvedHtml, currentVersionNumber, diffParagraphs);
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