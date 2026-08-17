using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class CommentDisplayService(
    IUserRepository userRepository,
    IPassageAnchorService passageAnchorService) : ICommentDisplayService
{
    public async Task<IReadOnlyList<CommentDisplayDto>> GetCommentDisplayDataAsync(
        IReadOnlyList<Comment> comments,
        Guid currentUserId,
        Guid projectAuthorId,
        bool currentUserIsModerator,
        CancellationToken ct = default)
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
            var author = await userRepository.GetByIdAsync(authorId, ct);
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
                anchorsById[anchorId] = await passageAnchorService.GetByIdAsync(anchorId, currentUserId, ct);
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

            var auditUser = await userRepository.GetByIdAsync(auditUserId, ct);
            authorNames[auditUserId] = auditUser?.DisplayName ?? "Unknown";
        }

        return visibleComments
            .Select(comment =>
            {
                var hasChildren = commentsByParentId.ContainsKey(comment.Id);
                var canDelete   = comment.AuthorId == currentUserId && !hasChildren;
                anchorsById.TryGetValue(comment.PassageAnchorId ?? Guid.Empty, out var passageAnchor);

                return new CommentDisplayDto
                {
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
}
