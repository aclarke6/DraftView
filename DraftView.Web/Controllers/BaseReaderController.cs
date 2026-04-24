using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Models;

namespace DraftView.Web.Controllers;

/// <summary>
/// Shared base for DesktopReaderController and MobileReaderController.
/// Contains all dependencies, helper methods, and POST actions.
/// GET actions are implemented in the derived controllers.
/// </summary>
[Authorize(Roles = "BetaReader,Author")]
#pragma warning disable CS9107
public abstract class BaseReaderController(
    IProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ICommentService commentService,
    IReadingProgressService progressService,
    IUserRepository userRepository,
    IReaderAccessRepository readerAccessRepo,
    IHumanOverrideService humanOverrideService,
    IPassageAnchorService passageAnchorService,
    ILogger logger) : BaseController(userRepository)
{
    protected readonly IProjectRepository ProjectRepo      = projectRepo;
    protected readonly ISectionRepository          SectionRepo      = sectionRepo;
    protected readonly ICommentService             CommentService   = commentService;
    protected readonly IReadingProgressService     ProgressService  = progressService;
    protected readonly IReaderAccessRepository     ReaderAccessRepo = readerAccessRepo;
    protected readonly IHumanOverrideService       HumanOverrideService = humanOverrideService;
    protected readonly IPassageAnchorService       PassageAnchorService = passageAnchorService;

    // -----------------------------------------------------------------------
    // POST: AddComment
    // Optional ReturnSceneId: if supplied, redirects to Read(sceneId) for
    // mobile; otherwise redirects to Read(chapterId) for desktop.
    // -----------------------------------------------------------------------
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
                await CommentService.CreateReplyAsync(
                    model.ParentCommentId.Value, user.Id, model.Body, visibility);
            else
                await CommentService.CreateRootCommentAsync(
                    model.SectionId,
                    user.Id,
                    model.Body,
                    visibility,
                    model.PassageAnchorRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add comment for user {UserId}", user.Id);
            TempData["Error"] = "Failed to save comment.";
        }

        var section   = await SectionRepo.GetByIdAsync(model.SectionId);
        var chapterId = section?.NodeType == NodeType.Folder
            ? section.Id
            : section?.ParentId ?? model.SectionId;

        string anchor;
        if (section?.NodeType == NodeType.Folder)
        {
            // Comment posted against the chapter folder itself → chapter comments section
            anchor = "#chapter-comments";
        }
        else
        {
            var sceneId = model.ReturnSceneId ?? section?.Id;
            anchor = sceneId.HasValue ? "#scene-" + sceneId.Value : string.Empty;
        }

        if (IsMobile())
        {
            var targetSceneId = model.ReturnSceneId ?? section?.Id;
            if (targetSceneId.HasValue)
                return RedirectToAction("Read", new
                {
                    id = targetSceneId.Value
                });

            return RedirectToAction("Scenes", new
            {
                chapterId
            });
        }

        var url = Url.Action("Read", new
        {
            id = chapterId
        }) + anchor;

        return Redirect(url!);
    }

    // -----------------------------------------------------------------------
    // POST: SetCommentStatus
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommentStatus(Guid commentId, Guid chapterId, Guid sceneId, CommentStatus status)
    {
        var user = await GetCurrentUserAsync();
        if (user is null || user.Role != Role.Author)
            return Forbid();

        try
        {
            await CommentService.SetCommentStatusAsync(commentId, user.Id, status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set comment status {CommentId}", commentId);
            TempData["Error"] = ex.Message;
        }

        var anchor = sceneId == chapterId ? "chapter-comments" : "scene-" + sceneId;
        return Redirect(Url.Action("Read", new {id = chapterId}) + "#" + anchor);
    }

    // -----------------------------------------------------------------------
    // POST: EditComment
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComment(Guid commentId, Guid chapterId, string body)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        try
        {
            await CommentService.EditCommentAsync(commentId, user.Id, body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to edit comment {CommentId} for user {UserId}", commentId, user.Id);
            TempData["Error"] = "Failed to update comment.";
        }

        return RedirectToAction("Read", new { id = chapterId });
    }

    // -----------------------------------------------------------------------
    // POST: DeleteComment
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid commentId, Guid chapterId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        await CommentService.SoftDeleteCommentAsync(commentId, user.Id);
        return RedirectToAction("Read", new { id = chapterId });
    }

    // -----------------------------------------------------------------------
    // POST: ModerateDeleteComment
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateDeleteComment(Guid commentId, Guid chapterId)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        await CommentService.ModerateDeleteCommentAsync(commentId, user.Id);
        return RedirectToAction("Read", new { id = chapterId });
    }

    // -----------------------------------------------------------------------
    // POST: RejectPassageAnchor
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectPassageAnchor(Guid anchorId, Guid chapterId, Guid sceneId, string? reason)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        try
        {
            await HumanOverrideService.RejectAsync(anchorId, user.Id, reason);
        }
        catch (UnauthorisedOperationException)
        {
            return Forbid();
        }
        catch (InvariantViolationException ex)
        {
            logger.LogError(ex, "Invalid reject request for passage anchor {AnchorId}", anchorId);
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reject passage anchor {AnchorId}", anchorId);
            TempData["Error"] = ex.Message;
        }

        return RedirectBackToReader(chapterId, sceneId);
    }

    // -----------------------------------------------------------------------
    // POST: RelinkPassageAnchor
    // -----------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RelinkPassageAnchor(
        Guid anchorId,
        Guid chapterId,
        Guid sceneId,
        CreatePassageAnchorRequest passageAnchorRequest)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Forbid();

        try
        {
            await HumanOverrideService.RelinkAsync(anchorId, passageAnchorRequest, user.Id);
        }
        catch (UnauthorisedOperationException)
        {
            return Forbid();
        }
        catch (InvariantViolationException ex)
        {
            logger.LogError(ex, "Invalid relink request for passage anchor {AnchorId}", anchorId);
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to relink passage anchor {AnchorId}", anchorId);
            TempData["Error"] = ex.Message;
        }

        return RedirectBackToReader(chapterId, sceneId);
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------
    protected async Task<IReadOnlyList<CommentDisplayViewModel>> BuildCommentDisplayModelsAsync(
        IReadOnlyList<Comment> comments,
        Guid currentUserId,
        Guid projectAuthorId,
        bool currentUserIsModerator)
    {
        var visibleComments = comments
            .Where(c => !c.IsSoftDeleted)
            .ToList();

        var commentsByParentId = visibleComments
            .Where(c => c.ParentCommentId.HasValue)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var authorIds   = visibleComments.Select(c => c.AuthorId).Distinct().ToList();
        var authorNames = new Dictionary<Guid, string>();

        foreach (var authorId in authorIds)
        {
            var author = await userRepository.GetByIdAsync(authorId);
            authorNames[authorId] = author?.DisplayName ?? "Unknown";
        }

        var anchorIds = visibleComments
            .Where(c => c.PassageAnchorId.HasValue)
            .Select(c => c.PassageAnchorId!.Value)
            .Distinct()
            .ToList();

        var anchorsById = new Dictionary<Guid, PassageAnchorDto?>();
        foreach (var anchorId in anchorIds)
        {
            try
            {
                anchorsById[anchorId] = await PassageAnchorService.GetByIdAsync(anchorId, currentUserId);
            }
            catch
            {
                anchorsById[anchorId] = null;
            }
        }

        var auditUserIds = anchorsById.Values
            .Where(anchor => anchor is not null)
            .SelectMany(anchor => new[]
            {
                anchor!.CurrentMatch?.ResolvedByUserId,
                anchor.Rejection?.RejectedByUserId
            })
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        foreach (var auditUserId in auditUserIds)
        {
            if (authorNames.ContainsKey(auditUserId))
                continue;

            var auditUser = await userRepository.GetByIdAsync(auditUserId);
            authorNames[auditUserId] = auditUser?.DisplayName ?? "Unknown";
        }

        return visibleComments
            .Select(comment =>
            {
                var hasChildren = commentsByParentId.ContainsKey(comment.Id);
                var canDelete   = comment.AuthorId == currentUserId && !hasChildren;
                anchorsById.TryGetValue(comment.PassageAnchorId ?? Guid.Empty, out var passageAnchor);

                return new CommentDisplayViewModel {
                    Comment           = comment,
                    AuthorDisplayName = authorNames.TryGetValue(comment.AuthorId, out var name) ? name : "Unknown",
                    HasChildren       = hasChildren,
                    CanDelete         = canDelete,
                    CanEdit           = comment.AuthorId == currentUserId,
                    IsModerator       = currentUserIsModerator,
                    PassageAnchor     = passageAnchor,
                    CanOverridePassageAnchor = passageAnchor is not null &&
                        (comment.AuthorId == currentUserId || projectAuthorId == currentUserId),
                    PassageAnchorResolvedByDisplayName = passageAnchor?.CurrentMatch?.ResolvedByUserId is Guid resolvedById &&
                        authorNames.TryGetValue(resolvedById, out var resolvedByName)
                        ? resolvedByName
                        : null,
                    PassageAnchorRejectedByDisplayName = passageAnchor?.Rejection?.RejectedByUserId is Guid rejectedById &&
                        authorNames.TryGetValue(rejectedById, out var rejectedByName)
                        ? rejectedByName
                        : null
                };
            })
            .ToList();
    }

    protected static Section? GetTopLevelAncestor(Section section, IReadOnlyList<Section> all)
    {
        var lookup  = all.ToDictionary(s => s.Id);
        var current = section;
        while (current.ParentId.HasValue && lookup.TryGetValue(current.ParentId.Value, out var parent))
        {
            if (!parent.ParentId.HasValue)
                return current;
            current = parent;
        }
        return null;
    }

    protected static IReadOnlyList<string> BuildBreadcrumb(Section section, IReadOnlyList<Section> all)
    {
        var lookup    = all.ToDictionary(s => s.Id);
        var crumbs    = new List<string>();
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

    protected static IReadOnlyList<ContentGroup> BuildContentGroups(
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

            var folderChildren      = all.Where(s => s.ParentId == child.Id && !s.IsSoftDeleted).ToList();
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
                    Heading        = string.Empty,
                    Depth          = 0,
                    ChapterSection = child,
                    Scenes         = new List<Section>(),
                    SubGroups      = new List<ContentGroup>()
                });
            }
        }
        return groups;
    }

    protected bool IsMobile()
    {
        var ua = Request.Headers.UserAgent.ToString();
        return !string.IsNullOrEmpty(ua) && (
            ua.Contains("Mobile",      StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("Android",     StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("iPhone",      StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("iPad",        StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("iPod",        StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("BlackBerry",  StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("IEMobile",    StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("Opera Mini", StringComparison.OrdinalIgnoreCase) ||
            ua.Contains("webOS",       StringComparison.OrdinalIgnoreCase));
    }

    protected static bool HasPublishedChapter(Section section, IReadOnlyList<Section> all)
    {
        if (section.NodeType == NodeType.Folder && section.IsPublished)
            return true;
        return all.Where(s => s.ParentId == section.Id && !s.IsSoftDeleted)
                  .Any(c => HasPublishedChapter(c, all));
    }

    /// <summary>
    /// Redirects back to the appropriate reader view after a human override action.
    /// </summary>
    protected IActionResult RedirectBackToReader(Guid chapterId, Guid sceneId)
    {
        if (IsMobile())
        {
            var targetSceneId = sceneId == Guid.Empty ? chapterId : sceneId;
            return RedirectToAction("Read", new { id = targetSceneId });
        }

        var anchor = sceneId == chapterId ? "chapter-comments" : "scene-" + sceneId;
        return Redirect(Url.Action("Read", new { id = chapterId }) + "#" + anchor);
    }
}
