using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

public interface ICommentService
{
    Task<Comment> CreateRootCommentAsync(
        Guid sectionId,
        Guid userId,
        string body,
        Visibility visibility,
        CreatePassageAnchorRequest? passageAnchorRequest = null,
        CancellationToken ct = default);
    Task<Comment> CreateReplyAsync(Guid parentCommentId, Guid userId, string body, Visibility requestedVisibility, CancellationToken ct = default);
    Task EditCommentAsync(Guid commentId, Guid userId, string newBody, CancellationToken ct = default);
    Task SetCommentStatusAsync(Guid commentId, Guid authorUserId, CommentStatus status, CancellationToken ct = default);
    Task SoftDeleteCommentAsync(Guid commentId, Guid actingUserId, CancellationToken ct = default);
    Task ModerateDeleteCommentAsync(Guid commentId, Guid moderatorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Comment>> GetThreadsForSectionAsync(Guid sectionId, Guid requestingUserId, CancellationToken ct = default);
}
