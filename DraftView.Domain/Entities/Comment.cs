using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class Comment
{
    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public Guid? SectionVersionId { get; private set; }
    public string Body { get; private set; } = default!;
    public Visibility Visibility { get; private set; }
    public CommentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public bool IsSoftDeleted { get; private set; }
    public DateTime? SoftDeletedAt { get; private set; }

    private Comment() { }

    public static Comment CreateRoot(
        Guid sectionId, Guid authorId, string body,
        Visibility visibility, bool isReaderComment = true,
        Guid? sectionVersionId = null)
    {
        ValidateBody(body);
        return new Comment
        {
            Id               = Guid.NewGuid(),
            SectionId        = sectionId,
            AuthorId         = authorId,
            ParentCommentId  = null,
            SectionVersionId = sectionVersionId,
            Body             = body.Trim(),
            Visibility       = visibility,
            Status           = isReaderComment ? CommentStatus.New : CommentStatus.AuthorReply,
            CreatedAt        = DateTime.UtcNow,
            IsSoftDeleted    = false
        };
    }

    public static Comment CreateReply(
        Guid sectionId, Guid authorId, Guid parentCommentId,
        Visibility parentVisibility, string body, Visibility requestedVisibility,
        Guid? sectionVersionId = null)
    {
        ValidateBody(body);
        var effectiveVisibility = parentVisibility == Visibility.Private
            ? Visibility.Private
            : requestedVisibility;
        return new Comment
        {
            Id               = Guid.NewGuid(),
            SectionId        = sectionId,
            AuthorId         = authorId,
            ParentCommentId  = parentCommentId,
            SectionVersionId = sectionVersionId,
            Body             = body.Trim(),
            Visibility       = effectiveVisibility,
            Status           = CommentStatus.AuthorReply,
            CreatedAt        = DateTime.UtcNow,
            IsSoftDeleted    = false
        };
    }

    public static Comment CreateForImport(
        Guid sectionId, Guid authorId, string body,
        Visibility visibility, CommentStatus status, DateTime createdAt,
        Guid? parentCommentId = null,
        Guid? sectionVersionId = null)
    {
        ValidateBody(body);
        return new Comment
        {
            Id               = Guid.NewGuid(),
            SectionId        = sectionId,
            AuthorId         = authorId,
            ParentCommentId  = parentCommentId,
            SectionVersionId = sectionVersionId,
            Body             = body.Trim(),
            Visibility       = visibility,
            Status           = status,
            CreatedAt        = createdAt,
            IsSoftDeleted    = false
        };
    }

    public void Edit(string body)
    {
        if (IsSoftDeleted)
            throw new InvariantViolationException("I-EDIT-DELETED",
                "A soft-deleted comment may not be edited.");
        ValidateBody(body);
        Body     = body.Trim();
        EditedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsSoftDeleted) return;
        IsSoftDeleted = true;
        SoftDeletedAt = DateTime.UtcNow;
    }

    public void SetStatus(CommentStatus status)
    {
        if (status == CommentStatus.AuthorReply)
            throw new InvariantViolationException("I-COMMENT-STATUS-AUTHOR",
                "AuthorReply may only be set by the factory.");
        Status = status;
    }

    public void MarkDoneByReply()
    {
        if (Status == CommentStatus.New)
            Status = CommentStatus.Done;
    }

    public bool IsVisibleTo(Guid requestingUserId, Role requestingUserRole)
    {
        if (Visibility == Visibility.Public) return true;
        if (requestingUserRole == Role.Author) return true;
        return AuthorId == requestingUserId;
    }

    private static void ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvariantViolationException("I-07",
                "Comment body must not be null or whitespace.");
    }
}

