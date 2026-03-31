using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class EmailDeliveryLog
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string RecipientEmail { get; private set; } = default!;
    public EmailType EmailType { get; private set; }
    public EmailStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private EmailDeliveryLog() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public static EmailDeliveryLog Create(
        Guid recipientUserId,
        string recipientEmail,
        EmailType emailType,
        Guid? relatedEntityId)
    {
        return new EmailDeliveryLog
        {
            Id               = Guid.NewGuid(),
            RecipientUserId  = recipientUserId,
            RecipientEmail   = recipientEmail,
            EmailType        = emailType,
            Status           = EmailStatus.Pending,
            AttemptCount     = 0,
            RelatedEntityId  = relatedEntityId,
            CreatedAt        = DateTime.UtcNow
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    public void RecordAttempt(bool success, string? failureReason)
    {
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;

        if (success)
        {
            Status        = EmailStatus.Sent;
            SentAt        = DateTime.UtcNow;
            FailureReason = null;
        }
        else
        {
            Status        = EmailStatus.Retrying;
            FailureReason = failureReason;
        }
    }

    public void MarkFailed()
    {
        if (Status == EmailStatus.Sent)
            throw new InvariantViolationException("I-EMAIL-SENT",
                "A successfully sent email cannot be marked as failed.");

        Status = EmailStatus.Failed;
    }
}
