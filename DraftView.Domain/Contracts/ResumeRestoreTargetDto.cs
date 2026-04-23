using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Contracts;

/// <summary>
/// Resume-anchor restore metadata returned to Web for safe reader fallback handling.
/// </summary>
public sealed record ResumeRestoreTargetDto(
    Guid ResumeAnchorId,
    Guid SectionId,
    Guid? SectionVersionId,
    PassageAnchorStatus Status,
    bool HasTarget,
    int? StartOffset,
    int? EndOffset,
    string? MatchedText,
    int? ConfidenceScore,
    PassageAnchorMatchMethod? MatchMethod);
