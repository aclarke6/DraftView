namespace DraftView.Domain.Enumerations;

/// <summary>
/// Represents the author's subscription tier.
/// Determines version retention limits per section.
/// Billing integration is deferred — tier is currently fixed at Free.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Paid = 1,
    Ultimate = 2
}
