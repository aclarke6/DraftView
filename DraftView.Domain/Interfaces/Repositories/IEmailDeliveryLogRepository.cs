using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IEmailDeliveryLogRepository
{
    Task<EmailDeliveryLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EmailDeliveryLog>> GetFailedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmailDeliveryLog>> GetRetryingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmailDeliveryLog>> GetPendingDigestAsync(Guid authorId, CancellationToken ct = default);
    Task AddAsync(EmailDeliveryLog log, CancellationToken ct = default);
}
