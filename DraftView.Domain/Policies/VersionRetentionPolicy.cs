using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Policies;

/// <summary>
/// Defines version retention limits per subscription tier.
/// The only place in the codebase where retention limits are defined.
/// </summary>
public static class VersionRetentionPolicy
{
    /// <summary>Sentinel value for unlimited retention.</summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>Maximum versions per section for Free tier authors.</summary>
    public const int FreeLimit = 3;

    /// <summary>Maximum versions per section for Paid tier authors.</summary>
    public const int PaidLimit = 10;

    /// <summary>
    /// Returns the maximum number of versions permitted per section for the given tier.
    /// </summary>
    public static int GetLimit(SubscriptionTier tier) => tier switch
    {
        SubscriptionTier.Free => FreeLimit,
        SubscriptionTier.Paid => PaidLimit,
        SubscriptionTier.Ultimate => Unlimited,
        _ => FreeLimit
    };

    /// <summary>
    /// Returns true when the existing version count has reached the limit for the given tier.
    /// </summary>
    public static bool IsAtLimit(int existingVersionCount, SubscriptionTier tier)
    {
        var limit = GetLimit(tier);
        return limit != Unlimited && existingVersionCount >= limit;
    }
}
