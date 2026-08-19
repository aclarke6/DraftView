using DraftView.Domain.Enumerations;

namespace DraftView.Application.Interfaces;

/// <summary>
/// Abstraction over a third-party billing provider.
/// No provider is selected yet; before billing is live the NullBillingProvider
/// is registered, which returns null for all queries.
/// </summary>
public interface IBillingProvider
{
    /// <summary>
    /// Returns the current active subscription tier for the given provider subscription id,
    /// or null when no active subscription is found or no provider is configured.
    /// </summary>
    Task<SubscriptionTier?> GetCurrentTierAsync(string providerSubscriptionId, CancellationToken ct = default);
}
