using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class NotificationService(
    IEmailDeliveryLogRepository logRepo,
    IUserRepository userRepo,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork) : INotificationService
{
    public async Task SendImmediateAsync(
        EmailType emailType, Guid recipientUserId, Guid? relatedEntityId,
        CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(recipientUserId, ct)
            ?? throw new EntityNotFoundException(nameof(User), recipientUserId);

        var log = EmailDeliveryLog.Create(
            recipientUserId, user.Email, emailType, relatedEntityId);

        await logRepo.AddAsync(log, ct);

        try
        {
            var subject = GetSubject(emailType);
            var body    = GetBody(emailType, relatedEntityId);

            await emailSender.SendAsync(user.Email, user.DisplayName, subject, body, ct);
            log.RecordAttempt(success: true, failureReason: null);
        }
        catch (Exception ex)
        {
            log.RecordAttempt(success: false, failureReason: ex.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendDigestAsync(Guid authorId, CancellationToken ct = default)
    {
        var pending = await logRepo.GetPendingDigestAsync(authorId, ct);
        if (pending.Count == 0) return;

        var author = await userRepo.GetByIdAsync(authorId, ct)
            ?? throw new EntityNotFoundException(nameof(User), authorId);

        var digestLog = EmailDeliveryLog.Create(
            authorId, author.Email, EmailType.DigestNotification, null);

        await logRepo.AddAsync(digestLog, ct);

        try
        {
            var body = $"You have {pending.Count} new comment(s) on your manuscript.";
            await emailSender.SendAsync(
                author.Email, author.DisplayName,
                "DraftView - New comments digest", body, ct);

            digestLog.RecordAttempt(success: true, failureReason: null);
        }
        catch (Exception ex)
        {
            digestLog.RecordAttempt(success: false, failureReason: ex.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RetryFailedAsync(CancellationToken ct = default)
    {
        var retrying = await logRepo.GetRetryingAsync(ct);

        foreach (var log in retrying)
        {
            var user = await userRepo.GetByIdAsync(log.RecipientUserId, ct);
            if (user is null) continue;

            try
            {
                var subject = GetSubject(log.EmailType);
                var body    = GetBody(log.EmailType, log.RelatedEntityId);

                await emailSender.SendAsync(user.Email, user.DisplayName, subject, body, ct);
                log.RecordAttempt(success: true, failureReason: null);
            }
            catch (Exception ex)
            {
                log.RecordAttempt(success: false, failureReason: ex.Message);

                if (log.AttemptCount >= 5)
                    log.MarkFailed();
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<int> GetFailureCountAsync(CancellationToken ct = default)
    {
        var failed = await logRepo.GetFailedAsync(ct);
        return failed.Count;
    }

    private static string GetSubject(EmailType emailType) => emailType switch
    {
        EmailType.Invitation                => "You have been invited to review a manuscript",
        EmailType.CommentNotification       => "DraftView - New comment on your manuscript",
        EmailType.ReplyNotification         => "DraftView - New reply to your comment",
        EmailType.NewSectionNotification    => "DraftView - New section available to read",
        EmailType.SectionChangedNotification => "DraftView - A section has been updated",
        EmailType.DigestNotification        => "DraftView - New comments digest",
        _                                   => "DraftView notification"
    };

    private static string GetBody(EmailType emailType, Guid? relatedEntityId) => emailType switch
    {
        EmailType.Invitation             => "You have been invited to review a manuscript. Please follow your invitation link to get started.",
        EmailType.CommentNotification    => "A beta reader has left a new comment on your manuscript.",
        EmailType.ReplyNotification      => "Someone has replied to a comment thread you are participating in.",
        EmailType.NewSectionNotification => "A new section has been published for your review.",
        EmailType.SectionChangedNotification => "A section you have read has been updated with new content.",
        EmailType.DigestNotification     => "You have new comments on your manuscript.",
        _                                => "You have a new notification on DraftView."
    };
}
