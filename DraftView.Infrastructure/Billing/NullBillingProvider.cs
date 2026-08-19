using DraftView.Application.Interfaces;
using DraftView.Domain.Enumerations;

namespace DraftView.Infrastructure.Billing;

/// <summary>
/// No-op billing provider registered before a real billing integration is selected.
/// Always returns null (no active subscription), which callers treat as pre-billing Free Tier.
/// Replace with a real implementation (Stripe, Paddle, etc.) in MT-Sprint-2 billing rollout.
/// </summary>
public class NullBillingProvider : IBillingProvider
{
    public Task<SubscriptionTier?> GetCurrentTierAsync(
        string providerSubscriptionId, CancellationToken ct = default) =>
        Task.FromResult<SubscriptionTier?>(null);
}
