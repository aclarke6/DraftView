using DraftView.Domain.Exceptions;

namespace DraftView.Domain.ValueObjects;

/// <summary>
/// Immutable audit record for a human rejection of a passage anchor location.
/// </summary>
public sealed class PassageAnchorRejection
{
    public Guid? TargetSectionVersionId { get; private set; }
    public Guid RejectedByUserId { get; private set; }
    public DateTime RejectedAt { get; private set; }
    public string? Reason { get; private set; }

    private PassageAnchorRejection() { }

    /// <summary>
    /// Creates a rejection audit record from the rejected match and actor.
    /// </summary>
    public static PassageAnchorRejection Create(
        PassageAnchorMatch rejectedMatch,
        Guid rejectedByUserId,
        string? reason = null)
    {
        if (rejectedMatch is null)
            throw new InvariantViolationException("I-ANCHOR-MATCH",
                "Rejected match is required.");

        if (rejectedByUserId == Guid.Empty)
            throw new InvariantViolationException("I-ANCHOR-ACTOR",
                "Rejecting a match requires an actor id.");

        return new PassageAnchorRejection
        {
            TargetSectionVersionId = rejectedMatch.TargetSectionVersionId,
            RejectedByUserId = rejectedByUserId,
            RejectedAt = DateTime.UtcNow,
            Reason = reason
        };
    }
}
