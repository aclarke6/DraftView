using DraftView.Domain.Exceptions;
using DraftView.Domain.Notifications;

namespace DraftView.Domain.Entities;

public sealed class AuthorNotification
{
    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Detail { get; private set; }
    public string? LinkUrl { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private AuthorNotification() { }

    public static AuthorNotification Create(
        Guid authorId,
        NotificationEventType eventType,
        string title,
        string? detail,
        string? linkUrl,
        DateTime occurredAt)
    {
        if (authorId == Guid.Empty)
            throw new InvariantViolationException("I-NOTIF-01",
                "AuthorId must not be empty.");
        if (string.IsNullOrWhiteSpace(title))
            throw new InvariantViolationException("I-NOTIF-02",
                "Title must not be null or whitespace.");
        return new AuthorNotification
        {
            Id         = Guid.NewGuid(),
            AuthorId   = authorId,
            EventType  = eventType,
            Title      = title.Trim(),
            Detail     = detail,
            LinkUrl    = linkUrl,
            OccurredAt = occurredAt
        };
    }
}
