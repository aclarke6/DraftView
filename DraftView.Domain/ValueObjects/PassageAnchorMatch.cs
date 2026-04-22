using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.ValueObjects;

/// <summary>
/// Derived current location for a passage anchor in target content.
/// </summary>
public sealed class PassageAnchorMatch
{
    public Guid? TargetSectionVersionId { get; private set; }
    public int StartOffset { get; private set; }
    public int EndOffset { get; private set; }
    public string MatchedText { get; private set; } = default!;
    public int ConfidenceScore { get; private set; }
    public PassageAnchorMatchMethod MatchMethod { get; private set; }
    public DateTime ResolvedAt { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? Reason { get; private set; }

    private PassageAnchorMatch() { }

    /// <summary>
    /// Creates the current best known match for a passage anchor.
    /// </summary>
    public static PassageAnchorMatch Create(
        Guid? targetSectionVersionId,
        int startOffset,
        int endOffset,
        string matchedText,
        int confidenceScore,
        PassageAnchorMatchMethod matchMethod,
        Guid? resolvedByUserId = null,
        string? reason = null)
    {
        if (startOffset < 0 || endOffset <= startOffset)
            throw new InvariantViolationException("I-ANCHOR-MATCH-OFFSET",
                "Match end offset must be greater than start offset.");

        if (string.IsNullOrWhiteSpace(matchedText))
            throw new InvariantViolationException("I-ANCHOR-MATCH-TEXT",
                "Matched text must not be null or whitespace.");

        if (confidenceScore is < 0 or > 100)
            throw new InvariantViolationException("I-ANCHOR-CONFIDENCE",
                "Confidence score must be between 0 and 100.");

        if (matchMethod is PassageAnchorMatchMethod.Rejected or PassageAnchorMatchMethod.Orphaned)
            throw new InvariantViolationException("I-ANCHOR-MATCH-METHOD",
                "Rejected and orphaned are anchor states, not active match methods.");

        if (matchMethod == PassageAnchorMatchMethod.ManualRelink &&
            (!resolvedByUserId.HasValue || resolvedByUserId.Value == Guid.Empty))
            throw new InvariantViolationException("I-ANCHOR-ACTOR",
                "Manual relink requires an actor id.");

        if (resolvedByUserId == Guid.Empty)
            throw new InvariantViolationException("I-ANCHOR-ACTOR",
                "Resolved-by user id must not be empty.");

        return new PassageAnchorMatch
        {
            TargetSectionVersionId = targetSectionVersionId,
            StartOffset = startOffset,
            EndOffset = endOffset,
            MatchedText = matchedText,
            ConfidenceScore = confidenceScore,
            MatchMethod = matchMethod,
            ResolvedAt = DateTime.UtcNow,
            ResolvedByUserId = resolvedByUserId,
            Reason = reason
        };
    }
}
