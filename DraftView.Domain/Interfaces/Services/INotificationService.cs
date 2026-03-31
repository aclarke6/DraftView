using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

public interface INotificationService
{
    Task SendImmediateAsync(EmailType emailType, Guid recipientUserId, Guid? relatedEntityId, CancellationToken ct = default);
    Task SendDigestAsync(Guid authorId, CancellationToken ct = default);
    Task RetryFailedAsync(CancellationToken ct = default);
    Task<int> GetFailureCountAsync(CancellationToken ct = default);
}
