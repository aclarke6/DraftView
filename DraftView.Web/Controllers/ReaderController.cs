using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;

namespace DraftView.Web.Controllers;

[Authorize]
#pragma warning disable CS9107
public class ReaderController(
    IScrivenerProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ICommentService commentService,
    IReadingProgressService progressService,
    IUserRepository userRepository,
    ILogger<ReaderController> logger) : BaseController(userRepository)
{
    public async Task<IActionResult> Dashboard()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var project = await projectRepo.GetReaderActiveProjectAsync();
        if (project is null)
            return View(new ReaderDashboardViewModel { ProjectName = null });

        var allSections = await sectionRepo.GetByProjectIdAsync(project.Id);
        var folderChildIds = allSections
            .Where(s => s.NodeType == NodeType.Folder && s.ParentId.HasValue)
            .Select(s => s.ParentId!.Value)
            .ToHashSet();

        var publishedChapters = allSections
            .Where(s => s.NodeType == NodeType.Folder && s.IsPublished && !s.IsSoftDeleted
                        && !folderChildIds.Contains(s.Id))
            .OrderBy(s => s.SortOrder)
            .ToList();

        var chaptersWithProgress = new List<ChapterProgressViewModel>();
        foreach (var chapter in publishedChapters)
        {
            var hasRead = await progressService.HasReadSectionAsync(user.Id, chapter.Id);
            chaptersWithProgress.Add(new ChapterProgressViewModel {
                Chapter = chapter,
                HasRead = hasRead
            });
        }

        return View(new ReaderDashboardViewModel {
            ProjectName = project.Name,
            PublishedChapters = chaptersWithProgress,
            TotalChapters = publishedChapters.Count,
            ReadChapters = chaptersWithProgress.Count(c => c.HasRead)
        });
    }

    public async Task<IActionResult> Index() => RedirectToAction("Dashboard");

    public async Task<IActionResult> Browse(Guid id)
    {
        var project = await projectRepo.GetReaderActiveProjectAsync();
        if (project is null)
            return View("NoActiveProject");

        var allSections = await sectionRepo.GetByProjectIdAsync(project.Id);
        var topSection = allSections.FirstOrDefault(s => s.Id == id);
        if (topSection is null)
            return NotFound();

        return View(new SectionContentsViewModel {
            TopLevelSection = topSection,
            Groups = BuildContentGroups(topSection, allSections),
            ProjectName = project.Name
        });
    }

    public async Task<IActionResult> Read(Guid id)
    {
        var chapter = await sectionRepo.GetByIdAsync(id);
        if (chapter is null || !chapter.IsPublished)
            return NotFound();

        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var isModerator = user.Role == Role.Author;

        await progressService.RecordOpenAsync(id, user.Id);

        var project = await projectRepo.GetReaderActiveProjectAsync();
        var allSections = project is not null
            ? await sectionRepo.GetByProjectIdAsync(project.Id)
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
            await progressService.RecordOpenAsync(scene.Id, user.Id);

            var comments = await commentService.GetThreadsForSectionAsync(scene.Id, user.Id);
            var displayComments = await BuildCommentDisplayModelsAsync(comments, user.Id, isModerator);

            scenesWithComments.Add(new SceneWithComments {
                Scene = scene,
                Comments = displayComments
            });
        }

        var chapterCommentsRaw = await commentService.GetThreadsForSectionAsync(id, user.Id);
        var chapterComments = await BuildCommentDisplayModelsAsync(chapterCommentsRaw, user.Id, isModerator);

        var breadcrumb = BuildBreadcrumb(chapter, allSections);
        var topAncestor = GetTopLevelAncestor(chapter, allSections);

        SectionContentsViewModel? bookContents = null;
        if (topAncestor is not null)
        {
            bookContents = new SectionContentsViewModel {
                TopLevelSection = topAncestor,
                Groups = BuildContentGroups(topAncestor, allSections),
                ProjectName = project?.Name ?? string.Empty
            };
        }

        return View(new ChapterReadViewModel {
            Chapter = chapter,
            Breadcrumb = breadcrumb,
            Scenes = scenesWithComments,
            ChapterComments = chapterComments,
            BookContents = bookContents,
            ProjectName = project?.Name ?? string.Empty,
            CurrentUserId = user.Id,
            CurrentUserIsModerator = isModerator
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(AddCommentViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        var visibility = model.IsPrivate ? Visibility.Private : Visibility.Public;

        try
        {
            if (model.ParentCommentId.HasValue)
                await commentService.CreateReplyAsync(
                    model.ParentCommentId.Value, user.Id, model.Body, visibility);
            else
                await commentService.CreateRootCommentAsync(
                    model.SectionId, user.Id, model.Body, visibility);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add comment for user {UserId}", user.Id);
            TempData["Error"] = "Failed to save comment.";
        }

        var section = await sectionRepo.GetByIdAsync(model.SectionId);
        var chapterId = section?.NodeType == NodeType.Folder
            ? section.Id
            : section?.ParentId ?? model.SectionId;

        return RedirectToAction("Read", new
        {
            id = chapterId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(Guid commentId, Guid chapterId, string body)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        try
        {
            await commentService.EditCommentAsync(commentId, user.Id, body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to edit comment {CommentId} for user {UserId}", commentId, user.Id);
            TempData["Error"] = "Failed to update comment.";
        }

        return RedirectToAction("Read", new
        {
            id = chapterId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid commentId, Guid chapterId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        await commentService.SoftDeleteCommentAsync(commentId, user.Id);
        return RedirectToAction("Read", new
        {
            id = chapterId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateDeleteComment(Guid commentId, Guid chapterId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        await commentService.ModerateDeleteCommentAsync(commentId, user.Id);
        return RedirectToAction("Read", new
        {
            id = chapterId
        });
    }

    private async Task<IReadOnlyList<CommentDisplayViewModel>> BuildCommentDisplayModelsAsync(
     IReadOnlyList<Comment> comments,
     Guid currentUserId,
     bool currentUserIsModerator)
    {
        var visibleComments = comments
            .Where(c => !c.IsSoftDeleted)
            .ToList();

        var commentsByParentId = visibleComments
            .Where(c => c.ParentCommentId.HasValue)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var authorIds = visibleComments.Select(c => c.AuthorId).Distinct().ToList();
        var authorNames = new Dictionary<Guid, string>();

        foreach (var authorId in authorIds)
        {
            var author = await userRepository.GetByIdAsync(authorId);
            authorNames[authorId] = author?.DisplayName ?? "Unknown";
        }

        return visibleComments
            .Select(comment =>
            {
                var hasChildren = commentsByParentId.ContainsKey(comment.Id);
                var canDelete = comment.AuthorId == currentUserId && !hasChildren;

                return new CommentDisplayViewModel {
                    Comment = comment,
                    AuthorDisplayName = authorNames.TryGetValue(comment.AuthorId, out var name) ? name : "Unknown",
                    HasChildren = hasChildren,
                    CanDelete = canDelete,
                    CanEdit = comment.AuthorId == currentUserId,
                    IsModerator = currentUserIsModerator
                };
            })
            .ToList();
    }

    private static bool HasPublishedChapter(Section section, IReadOnlyList<Section> all)
    {
        if (section.NodeType == NodeType.Folder && section.IsPublished)
            return true;
        return all.Where(s => s.ParentId == section.Id && !s.IsSoftDeleted)
                  .Any(c => HasPublishedChapter(c, all));
    }

    private static Section? GetTopLevelAncestor(Section section, IReadOnlyList<Section> all)
    {
        var lookup = all.ToDictionary(s => s.Id);
        var current = section;
        while (current.ParentId.HasValue && lookup.TryGetValue(current.ParentId.Value, out var parent))
        {
            if (!parent.ParentId.HasValue)
                return current;
            current = parent;
        }
        return null;
    }

    private static IReadOnlyList<string> BuildBreadcrumb(Section section, IReadOnlyList<Section> all)
    {
        var lookup = all.ToDictionary(s => s.Id);
        var crumbs = new List<string>();
        var currentId = section.ParentId;
        while (currentId.HasValue && lookup.TryGetValue(currentId.Value, out var parent))
        {
            crumbs.Insert(0, parent.Title);
            currentId = parent.ParentId;
        }
        if (crumbs.Count > 0)
            crumbs.RemoveAt(0);
        return crumbs;
    }

    private static IReadOnlyList<ContentGroup> BuildContentGroups(
        Section parent, IReadOnlyList<Section> all)
    {
        var children = all
            .Where(s => s.ParentId == parent.Id && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToList();

        var groups = new List<ContentGroup>();
        foreach (var child in children)
        {
            if (child.NodeType != NodeType.Folder)
                continue;

            var folderChildren = all.Where(s => s.ParentId == child.Id && !s.IsSoftDeleted).ToList();
            var folderHasSubFolders = folderChildren.Any(s => s.NodeType == NodeType.Folder);

            if (folderHasSubFolders)
            {
                var subGroups = BuildContentGroups(child, all);
                if (subGroups.Any())
                    groups.Add(new ContentGroup { Heading = child.Title, Depth = 0, SubGroups = subGroups });
            }
            else if (child.IsPublished)
            {
                groups.Add(new ContentGroup {
                    Heading = string.Empty,
                    Depth = 0,
                    ChapterSection = child,
                    Scenes = new List<Section>(),
                    SubGroups = new List<ContentGroup>()
                });
            }
        }
        return groups;
    }
}



