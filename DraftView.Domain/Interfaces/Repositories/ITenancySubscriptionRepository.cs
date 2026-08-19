using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for TenancySubscription entities.
/// One subscription record per tenancy. Billing-provider details are stored here
/// once a billing provider is integrated (MT-Sprint-2 billing rollout phase).
/// </summary>
public interface ITenancySubscriptionRepository
{
    Task<TenancySubscription?> GetByTenancyIdAsync(Guid tenancyId, CancellationToken ct = default);
    Task AddAsync(TenancySubscription subscription, CancellationToken ct = default);
}
