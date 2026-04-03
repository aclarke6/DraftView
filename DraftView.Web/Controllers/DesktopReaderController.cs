using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;

namespace DraftView.Web.Controllers;

public class DesktopReaderController(
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ICommentService commentService,
    IReadingProgressService progressService,
    IUserRepository userRepository,
    IReaderAccessRepository readerAccessRepo,
    ILogger<DesktopReaderController> logger)
    : BaseReaderController(projectRepo, sectionRepo, commentService, progressService,
                           userRepository, readerAccessRepo, logger)
{
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

    public IActionResult Index() => RedirectToAction("Dashboard");

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

    public async Task<IActionResult> Read(Guid id)
    {
        var chapter = await SectionRepo.GetByIdAsync(id);
        if (chapter is null || !chapter.IsPublished)
            return NotFound();

        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

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
            await ProgressService.RecordOpenAsync(scene.Id, user.Id);
            var comments        = await CommentService.GetThreadsForSectionAsync(scene.Id, user.Id);
            var displayComments = await BuildCommentDisplayModelsAsync(comments, user.Id, isModerator);
            scenesWithComments.Add(new SceneWithComments { Scene = scene, Comments = displayComments });
        }

        var chapterCommentsRaw = await CommentService.GetThreadsForSectionAsync(id, user.Id);
        var chapterComments    = await BuildCommentDisplayModelsAsync(chapterCommentsRaw, user.Id, isModerator);
        var breadcrumb         = BuildBreadcrumb(chapter, allSections);
        var topAncestor        = GetTopLevelAncestor(chapter, allSections);

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
            CurrentUserIsModerator = isModerator
        });
    }
}