using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

/// <summary>
/// Represents the subscription tier state for a Tenancy.
/// ProviderSubscriptionId is null until a billing provider is integrated.
/// Before billing is live, all tenancies are created on Free tier semantics
/// but MaxBetaReaderCount is controlled by the Tenancy entity directly.
/// </summary>
public sealed class TenancySubscription
{
    public Guid Id { get; private set; }
    public Guid TenancyId { get; private set; }
    public SubscriptionTier Tier { get; private set; }

    /// <summary>External identifier from the billing provider. Null until billing is live.</summary>
    public string? ProviderSubscriptionId { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private TenancySubscription() { }

    public static TenancySubscription Create(Guid tenancyId, SubscriptionTier tier)
    {
        if (tenancyId == Guid.Empty)
            throw new InvariantViolationException("I-TSUB-TENANCY",
                "TenancySubscription must reference a valid tenancy.");

        return new TenancySubscription
        {
            Id        = Guid.NewGuid(),
            TenancyId = tenancyId,
            Tier      = tier,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateTier(SubscriptionTier tier)
    {
        Tier      = tier;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records the external billing-provider subscription identifier.
    /// Called when a billing provider is integrated and creates a subscription record.
    /// </summary>
    public void SetProviderSubscriptionId(string id)
    {
        ProviderSubscriptionId = id;
        UpdatedAt              = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive  = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
