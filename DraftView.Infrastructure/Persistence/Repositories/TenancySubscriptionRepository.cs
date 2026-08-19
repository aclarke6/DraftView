using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class TenancySubscriptionRepository(DraftViewDbContext db) : ITenancySubscriptionRepository
{
    public Task<TenancySubscription?> GetByTenancyIdAsync(Guid tenancyId, CancellationToken ct = default) =>
        db.TenancySubscriptions.FirstOrDefaultAsync(s => s.TenancyId == tenancyId, ct);

    public async Task AddAsync(TenancySubscription subscription, CancellationToken ct = default) =>
        await db.TenancySubscriptions.AddAsync(subscription, ct);
}
