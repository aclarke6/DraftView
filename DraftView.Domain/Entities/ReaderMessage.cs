using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Represents a personal note sent by the author to a specific reader via email.
/// Stored as a sent log; does not drive any in-app notification.
/// </summary>
public sealed class ReaderMessage
{
    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid RecipientId { get; private set; }
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public DateTime SentAt { get; private set; }

    private ReaderMessage() { }

    /// <summary>
    /// Creates a new ReaderMessage. Subject and body must not be null or whitespace.
    /// </summary>
    /// <exception cref="InvariantViolationException">Thrown when subject or body is null or whitespace.</exception>
    public static ReaderMessage Create(Guid authorId, Guid recipientId, string subject, string body, DateTime sentAt)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvariantViolationException("I-MSG-SUBJECT", "Subject must not be null or whitespace.");
        if (string.IsNullOrWhiteSpace(body))
            throw new InvariantViolationException("I-MSG-BODY", "Body must not be null or whitespace.");

        return new ReaderMessage
        {
            Id          = Guid.NewGuid(),
            AuthorId    = authorId,
            RecipientId = recipientId,
            Subject     = subject.Trim(),
            Body        = body.Trim(),
            SentAt      = sentAt
        };
    }
}
