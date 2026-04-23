using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Contracts;

/// <summary>
/// Current resolved-location metadata returned for a passage anchor.
/// </summary>
public sealed record PassageAnchorMatchDto(
    Guid? TargetSectionVersionId,
    int StartOffset,
    int EndOffset,
    string MatchedText,
    int ConfidenceScore,
    PassageAnchorMatchMethod MatchMethod,
    DateTime ResolvedAt,
    Guid? ResolvedByUserId,
    string? Reason);
