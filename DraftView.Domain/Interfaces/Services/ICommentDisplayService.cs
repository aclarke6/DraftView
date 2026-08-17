using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Fully-resolved display data for a single comment: author name, passage
/// anchor state, and the current user's permissions for it. Mirrors the
/// shape the reader Web layer needs to render its comment ViewModels
/// without performing any further lookups or business rules.
/// </summary>
public sealed class CommentDisplayDto
{
    public required Comment Comment { get; init; }
    public required string AuthorDisplayName { get; init; }
    public required bool HasChildren { get; init; }
    public required bool CanDelete { get; init; }
    public required bool CanEdit { get; init; }
    public required bool IsModerator { get; init; }
    public PassageAnchorDto? PassageAnchor { get; init; }
    public required bool CanOverridePassageAnchor { get; init; }
    public string? PassageAnchorResolvedByDisplayName { get; init; }
    public string? PassageAnchorRejectedByDisplayName { get; init; }
}

public interface ICommentDisplayService
{
    /// <summary>
    /// Builds display data for a list of comments: filters soft-deleted
    /// comments, resolves author display names, resolves passage anchors
    /// (and their audit user names), and determines edit/delete/override
    /// permissions for the current user.
    /// </summary>
    Task<IReadOnlyList<CommentDisplayDto>> GetCommentDisplayDataAsync(
        IReadOnlyList<Comment> comments,
        Guid currentUserId,
        Guid projectAuthorId,
        bool currentUserIsModerator,
        CancellationToken ct = default);
}
