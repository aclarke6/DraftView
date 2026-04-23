using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Contracts;

/// <summary>
/// Service result for a passage anchor, including original snapshot and current match.
/// </summary>
public sealed record PassageAnchorDto(
    Guid Id,
    Guid SectionId,
    Guid? OriginalSectionVersionId,
    PassageAnchorPurpose Purpose,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    PassageAnchorStatus Status,
    DateTime? UpdatedAt,
    PassageAnchorSnapshotDto OriginalSnapshot,
    PassageAnchorMatchDto? CurrentMatch);
