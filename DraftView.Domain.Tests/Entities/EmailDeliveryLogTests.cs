using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class EmailDeliveryLogTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsLog()
    {
        var before = DateTime.UtcNow;

        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.Invitation, null);

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(UserId, log.RecipientUserId);
        Assert.Equal("test@example.com", log.RecipientEmail);
        Assert.Equal(EmailType.Invitation, log.EmailType);
        Assert.Equal(EmailStatus.Pending, log.Status);
        Assert.Equal(0, log.AttemptCount);
        Assert.Null(log.LastAttemptAt);
        Assert.Null(log.SentAt);
        Assert.Null(log.FailureReason);
        Assert.Null(log.RelatedEntityId);
        Assert.True(log.CreatedAt >= before);
    }

    [Fact]
    public void Create_WithRelatedEntityId_SetsRelatedEntityId()
    {
        var relatedId = Guid.NewGuid();

        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.CommentNotification, relatedId);

        Assert.Equal(relatedId, log.RelatedEntityId);
    }

    // ---------------------------------------------------------------------------
    // RecordAttempt
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordAttempt_Success_SetsSentAndUpdatesCount()
    {
        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.Invitation, null);
        var before = DateTime.UtcNow;

        log.RecordAttempt(success: true, failureReason: null);

        Assert.Equal(EmailStatus.Sent, log.Status);
        Assert.Equal(1, log.AttemptCount);
        Assert.NotNull(log.SentAt);
        Assert.True(log.SentAt >= before);
        Assert.Null(log.FailureReason);
    }

    [Fact]
    public void RecordAttempt_Failure_SetsRetryingAndRecordsReason()
    {
        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.Invitation, null);

        log.RecordAttempt(success: false, failureReason: "SMTP timeout.");

        Assert.Equal(EmailStatus.Retrying, log.Status);
        Assert.Equal(1, log.AttemptCount);
        Assert.Null(log.SentAt);
        Assert.Equal("SMTP timeout.", log.FailureReason);
    }

    [Fact]
    public void RecordAttempt_MultipleFailures_IncrementsCount()
    {
        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.Invitation, null);

        log.RecordAttempt(success: false, failureReason: "Timeout.");
        log.RecordAttempt(success: false, failureReason: "Timeout.");
        log.RecordAttempt(success: false, failureReason: "Timeout.");

        Assert.Equal(3, log.AttemptCount);
    }

    // ---------------------------------------------------------------------------
    // MarkFailed
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkFailed_SetsStatusToFailed()
    {
        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.Invitation, null);
        log.RecordAttempt(success: false, failureReason: "Timeout.");

        log.MarkFailed();

        Assert.Equal(EmailStatus.Failed, log.Status);
    }

    [Fact]
    public void MarkFailed_WhenAlreadySent_ThrowsInvariantViolationException()
    {
        var log = EmailDeliveryLog.Create(UserId, "test@example.com", EmailType.Invitation, null);
        log.RecordAttempt(success: true, failureReason: null);

        var ex = Assert.Throws<InvariantViolationException>(() => log.MarkFailed());

        Assert.Equal("I-EMAIL-SENT", ex.InvariantCode);
    }
}
