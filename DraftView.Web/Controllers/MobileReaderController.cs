using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;

namespace DraftView.Web.Controllers;

#pragma warning disable CS9107
public class MobileReaderController(
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ICommentService commentService,
    IReadingProgressService progressService,
    IUserRepository userRepository,
    IReaderAccessRepository readerAccessRepo,
    ILogger<MobileReaderController> logger)
    : BaseReaderController(projectRepo, sectionRepo, commentService, progressService,
                           userRepository, readerAccessRepo, logger)
{
    // -----------------------------------------------------------------------
    // GET: /Reader/Dashboard -> redirects to MobileChapters for first project
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

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

    public IActionResult Index() => RedirectToAction("Dashboard");

    // -----------------------------------------------------------------------
    // GET: /Reader/Chapters?projectId=...
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
    // GET: /Reader/Scenes?chapterId=...
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
    // GET: /Reader/Read/{id} -- single scene
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Read(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

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

        var allSections = await SectionRepo.GetByProjectIdAsync(project.Id);

        var siblingScenes = allSections
            .Where(s => s.ParentId == chapter.Id &&
                        s.NodeType == NodeType.Document &&
                        s.IsPublished && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var currentIndex = siblingScenes.FindIndex(s => s.Id == id);
        var prevSceneId  = currentIndex > 0
            ? siblingScenes[currentIndex - 1].Id
            : (Guid?)null;
        var nextSceneId  = currentIndex >= 0 && currentIndex < siblingScenes.Count - 1
            ? siblingScenes[currentIndex + 1].Id
            : (Guid?)null;

        var commentsRaw = await CommentService.GetThreadsForSectionAsync(id, user.Id);
        var comments    = await BuildCommentDisplayModelsAsync(commentsRaw, user.Id, isModerator);

        return View("MobileRead", new MobileReadViewModel {
            Scene                  = scene,
            Chapter                = chapter,
            ProjectName            = project.Name,
            Comments               = comments,
            PrevSceneId            = prevSceneId,
            NextSceneId            = nextSceneId,
            CurrentUserId          = user.Id,
            CurrentUserIsModerator = isModerator
        });
    }
}