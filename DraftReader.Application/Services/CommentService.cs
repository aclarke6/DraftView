using DraftReader.Domain.Entities;
using DraftReader.Domain.Enumerations;
using DraftReader.Domain.Exceptions;
using DraftReader.Domain.Interfaces.Repositories;
using DraftReader.Domain.Interfaces.Services;

namespace DraftReader.Application.Services;

public class CommentService(
    ICommentRepository commentRepo,
    ISectionRepository sectionRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork) : ICommentService
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
        var comment = Comment.CreateRoot(sectionId, userId, body, visibility,
            isReaderComment: user.Role == Role.BetaReader);
        await commentRepo.AddAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);
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
        var reply = Comment.CreateReply(
            parent.SectionId, userId, parentCommentId,
            parent.Visibility, body, requestedVisibility);
        if (user.Role == Role.Author && parent.Status != CommentStatus.AuthorReply)
            parent.MarkDoneByReply();
        await commentRepo.AddAsync(reply, ct);
        await unitOfWork.SaveChangesAsync(ct);
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
        if (comment.AuthorId != actingUserId && user.Role != Role.Author)
            throw new UnauthorisedOperationException(
                "Only the comment author or the platform author may delete a comment.");
        comment.SoftDelete();
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
}
