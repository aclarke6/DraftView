using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class EmailDeliveryLogRepository(DraftViewDbContext db) : IEmailDeliveryLogRepository
{
    public async Task<EmailDeliveryLog?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.EmailDeliveryLogs.FindAsync([id], ct);

    public async Task<IReadOnlyList<EmailDeliveryLog>> GetFailedAsync(CancellationToken ct = default) =>
        await db.EmailDeliveryLogs
            .Where(e => e.Status == EmailStatus.Failed)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EmailDeliveryLog>> GetRetryingAsync(CancellationToken ct = default) =>
        await db.EmailDeliveryLogs
            .Where(e => e.Status == EmailStatus.Retrying)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EmailDeliveryLog>> GetPendingDigestAsync(Guid authorId, CancellationToken ct = default) =>
        await db.EmailDeliveryLogs
            .Where(e => e.RecipientUserId == authorId &&
                        e.Status == EmailStatus.Pending &&
                        e.EmailType == EmailType.CommentNotification)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(EmailDeliveryLog log, CancellationToken ct = default) =>
        await db.EmailDeliveryLogs.AddAsync(log, ct);
}
