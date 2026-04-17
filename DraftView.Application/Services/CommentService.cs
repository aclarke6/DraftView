using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

public class CommentService(
    ICommentRepository commentRepo,
    ISectionRepository sectionRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork,
    IAuthorNotificationRepository notificationRepo,
    ISectionVersionRepository sectionVersionRepo) : ICommentService
{
    public async Task<Comment> CreateRootCommentAsync(
        Guid sectionId, Guid userId, string body, Visibility visibility,
        CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(User), userId);
        if (user.Role == Role.BetaReader && !section.IsPublished)
            throw new UnauthorisedOperationException(
                "Beta readers may only comment on published sections.");

        var latestVersion = await sectionVersionRepo.GetLatestAsync(sectionId, ct);
        var comment = Comment.CreateRoot(sectionId, userId, body, visibility,
            isReaderComment: user.Role == Role.BetaReader, sectionVersionId: latestVersion?.Id);
        await commentRepo.AddAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        if (user.Role == Role.BetaReader)
        {
            var author = await userRepo.GetAuthorAsync(ct);
            if (author is not null)
            {
                var notification = AuthorNotification.Create(
                    author.Id,
                    NotificationEventType.NewComment,
                    $"{user.DisplayName} commented on \"{section.Title}\"",
                    Truncate(body),
                    $"/Author/Section/{sectionId}",
                    DateTime.UtcNow);
                await notificationRepo.AddAsync(notification, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
        }

        return comment;
    }

    public async Task<Comment> CreateReplyAsync(
        Guid parentCommentId, Guid userId, string body, Visibility requestedVisibility,
        CancellationToken ct = default)
    {
        var parent = await commentRepo.GetByIdAsync(parentCommentId, ct)
            ?? throw new EntityNotFoundException(nameof(Comment), parentCommentId);
        if (parent.IsSoftDeleted)
            throw new InvariantViolationException("I-17",
                "Cannot reply to a soft-deleted comment.");
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(User), userId);
        var section = await sectionRepo.GetByIdAsync(parent.SectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), parent.SectionId);
        if (user.Role == Role.BetaReader && !section.IsPublished)
            throw new UnauthorisedOperationException(
                "Beta readers may only comment on published sections.");

        var latestVersion = await sectionVersionRepo.GetLatestAsync(parent.SectionId, ct);
        var reply = Comment.CreateReply(
            parent.SectionId, userId, parentCommentId,
            parent.Visibility, body, requestedVisibility, sectionVersionId: latestVersion?.Id);
        if (user.Role == Role.Author && parent.Status != CommentStatus.AuthorReply)
            parent.MarkDoneByReply();
        await commentRepo.AddAsync(reply, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var siteAuthor = await userRepo.GetAuthorAsync(ct);
        if (siteAuthor is not null && parent.AuthorId == siteAuthor.Id && user.Role == Role.BetaReader)
        {
            var notification = AuthorNotification.Create(
                siteAuthor.Id,
                NotificationEventType.ReplyToAuthor,
                $"{user.DisplayName} replied to your comment on \"{section.Title}\"",
                Truncate(body),
                $"/Author/Section/{parent.SectionId}",
                DateTime.UtcNow);
            await notificationRepo.AddAsync(notification, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return reply;
    }

    public async Task EditCommentAsync(
        Guid commentId, Guid userId, string newBody, CancellationToken ct = default)
    {
        var comment = await commentRepo.GetByIdAsync(commentId, ct)
            ?? throw new EntityNotFoundException(nameof(Comment), commentId);
        var user = await userRepo.GetByIdAsync(userId, ct)
            ?? throw new EntityNotFoundException(nameof(User), userId);

        // Authorisation rule:
        // Editing is strictly ownership-based.
        // Role.Author does NOT grant permission to edit other users' comments.
        if (comment.AuthorId != userId)
            throw new UnauthorisedOperationException(
                "Only the comment author may edit a comment.");

        comment.Edit(newBody);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetCommentStatusAsync(
        Guid commentId, Guid authorUserId, CommentStatus status, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(authorUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), authorUserId);
        if (user.Role != Role.Author)
            throw new UnauthorisedOperationException(
                "Only the author may set comment status.");
        var comment = await commentRepo.GetByIdAsync(commentId, ct)
            ?? throw new EntityNotFoundException(nameof(Comment), commentId);
        comment.SetStatus(status);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteCommentAsync(
        Guid commentId, Guid actingUserId, CancellationToken ct = default)
    {
        var comment = await commentRepo.GetByIdAsync(commentId, ct)
            ?? throw new EntityNotFoundException(nameof(Comment), commentId);
        var user = await userRepo.GetByIdAsync(actingUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), actingUserId);

        // Authorisation and deletion rule:
        // Deletion is strictly ownership-based.
        // Role.Author does NOT grant permission to delete other users' comments.
        // A comment or reply may be deleted only when it has no child replies.
        // Soft-deleted children still count as children for delete eligibility.
        if (comment.AuthorId != actingUserId)
            throw new UnauthorisedOperationException(
                "Only the comment author may delete a comment.");

        var children = await commentRepo.GetRepliesByParentIdAsync(comment.Id, ct);
        if (children.Count > 0)
            throw new InvariantViolationException(
                "I-COMMENT-HAS-CHILDREN",
                "A comment with child replies may not be deleted.");

        comment.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Moderator delete is a distinct operation from normal user delete.
    /// It will allow an Author acting as Moderator to delete any comment,
    /// including a comment with descendants, and will cascade through the subtree.
    /// </summary>
    /// <param name="commentId"></param>
    /// <param name="moderatorUserId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="EntityNotFoundException">
    /// Thrown if the comment or user is not found.
    /// </exception>
    /// <exception cref="UnauthorisedOperationException">
    /// Thrown if the user is not an Author.
    /// </exception>
    public async Task ModerateDeleteCommentAsync(
        Guid commentId, Guid moderatorUserId, CancellationToken ct = default)
    {
        var comment = await commentRepo.GetByIdAsync(commentId, ct)
            ?? throw new EntityNotFoundException(nameof(Comment), commentId);

        var user = await userRepo.GetByIdAsync(moderatorUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), moderatorUserId);

        // Moderator delete rule:
        // Only the Author role may perform a moderator delete.
        // Moderator delete may target any comment or reply and cascades through all descendants.
        if (user.Role != Role.Author)
            throw new UnauthorisedOperationException(
                "Only a moderator may perform a moderator delete.");

        await SoftDeleteSubTreeAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Returns all comments for a section (roots and replies), ordered by CreatedAt,
    /// filtered by visibility rules for the requesting user.
    /// </summary>
    public async Task<IReadOnlyList<Comment>> GetThreadsForSectionAsync(
        Guid sectionId, Guid requestingUserId, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(requestingUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), requestingUserId);
        var all = await commentRepo.GetAllBySectionIdAsync(sectionId, ct);
        return all.Where(c => c.IsVisibleTo(requestingUserId, user.Role)).ToList();
    }

    private static string Truncate(string body, int max = 80)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var t = body.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "\u2026";
    }

    /// <summary>
    /// Cascade soft delete helper for moderator delete.
    /// This will soft delete the current comment and then recurse through all descendants.
    /// </summary>
    private async Task SoftDeleteSubTreeAsync(Comment comment, CancellationToken ct)
    {
        // Idempotent: safe to call even if already soft deleted
        comment.SoftDelete();

        var children = await commentRepo.GetRepliesByParentIdAsync(comment.Id, ct);

        foreach (var child in children)
        {
            await SoftDeleteSubTreeAsync(child, ct);
        }
    }
}
