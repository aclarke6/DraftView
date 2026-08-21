namespace DraftView.Domain.Enumerations;

/// <summary>
/// The subscription tier for a Tenancy.
/// Limits apply per the billing plan; before billing is live all tenancies operate on
/// Free Tier semantics but with MaxBetaReaderCount=5 (pre-billing operational default).
/// </summary>
public enum SubscriptionTier
{
    /// <summary>Free: 3 beta readers, 1 active project.</summary>
    Free,

    /// <summary>Paid: 10 beta readers, unlimited active projects.</summary>
    Paid,

    /// <summary>Ultimate: unlimited beta readers and active projects.</summary>
    Ultimate
}
