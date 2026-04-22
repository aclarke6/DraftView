using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.ValueObjects;

namespace DraftView.Domain.Entities;

/// <summary>
/// Owns immutable original passage capture and the current trusted match state.
/// </summary>
public sealed class PassageAnchor
{
    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid? OriginalSectionVersionId { get; private set; }
    public PassageAnchorPurpose Purpose { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public PassageAnchorSnapshot OriginalSnapshot { get; private set; } = default!;
    public PassageAnchorMatch? CurrentMatch { get; private set; }
    public PassageAnchorStatus Status { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PassageAnchor() { }

    /// <summary>
    /// Creates an anchor for a selected passage in a section.
    /// </summary>
    public static PassageAnchor Create(
        Guid sectionId,
        Guid? originalSectionVersionId,
        PassageAnchorPurpose purpose,
        Guid createdByUserId,
        PassageAnchorSnapshot originalSnapshot)
    {
        if (sectionId == Guid.Empty)
            throw new InvariantViolationException("I-ANCHOR-SECTION",
                "Section id must not be empty.");

        if (createdByUserId == Guid.Empty)
            throw new InvariantViolationException("I-ANCHOR-ACTOR",
                "Created-by user id must not be empty.");

        if (originalSnapshot is null)
            throw new InvariantViolationException("I-ANCHOR-SNAPSHOT",
                "Original snapshot is required.");

        return new PassageAnchor
        {
            Id = Guid.NewGuid(),
            SectionId = sectionId,
            OriginalSectionVersionId = originalSectionVersionId,
            Purpose = purpose,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            OriginalSnapshot = originalSnapshot,
            Status = PassageAnchorStatus.Original
        };
    }

    /// <summary>
    /// Replaces the current automated match when no higher-authority human state blocks it.
    /// </summary>
    public void UpdateCurrentMatch(PassageAnchorMatch match)
    {
        if (match is null)
            throw new InvariantViolationException("I-ANCHOR-MATCH",
                "Current match is required.");

        if (Status == PassageAnchorStatus.UserRelinked &&
            match.MatchMethod != PassageAnchorMatchMethod.ManualRelink)
            throw new InvariantViolationException("I-ANCHOR-MANUAL",
                "Automated matches cannot overwrite a manual relink.");

        CurrentMatch = match;
        Status = GetStatusForMatchMethod(match.MatchMethod);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the anchor as orphaned and clears the current match.
    /// </summary>
    public void MarkOrphaned(string? reason = null)
    {
        CurrentMatch = null;
        Status = PassageAnchorStatus.Orphaned;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a human rejection of the current proposed location.
    /// </summary>
    public void Reject(Guid actorUserId, string? reason = null)
    {
        if (actorUserId == Guid.Empty)
            throw new InvariantViolationException("I-ANCHOR-ACTOR",
                "Rejecting a match requires an actor id.");

        CurrentMatch = null;
        Status = PassageAnchorStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a human-selected current location for the anchor.
    /// </summary>
    public void Relink(PassageAnchorMatch match, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty)
            throw new InvariantViolationException("I-ANCHOR-ACTOR",
                "Manual relink requires an actor id.");

        if (match is null)
            throw new InvariantViolationException("I-ANCHOR-MATCH",
                "Manual relink requires a match.");

        if (match.MatchMethod != PassageAnchorMatchMethod.ManualRelink)
            throw new InvariantViolationException("I-ANCHOR-MANUAL",
                "Manual relink requires a manual relink match.");

        CurrentMatch = match;
        Status = PassageAnchorStatus.UserRelinked;
        UpdatedAt = DateTime.UtcNow;
    }

    private static PassageAnchorStatus GetStatusForMatchMethod(PassageAnchorMatchMethod matchMethod)
    {
        return matchMethod switch
        {
            PassageAnchorMatchMethod.Exact => PassageAnchorStatus.Exact,
            PassageAnchorMatchMethod.Context => PassageAnchorStatus.Context,
            PassageAnchorMatchMethod.Fuzzy => PassageAnchorStatus.Fuzzy,
            PassageAnchorMatchMethod.Ai => PassageAnchorStatus.AiMatched,
            PassageAnchorMatchMethod.ManualRelink => PassageAnchorStatus.UserRelinked,
            _ => throw new InvariantViolationException("I-ANCHOR-MATCH-METHOD",
                "Unsupported active match method.")
        };
    }
}
