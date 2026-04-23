using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Contracts;

/// <summary>
/// Input contract for creating a passage anchor from a reader-visible selection.
/// </summary>
public sealed record CreatePassageAnchorRequest(
    Guid SectionId,
    Guid? OriginalSectionVersionId,
    PassageAnchorPurpose Purpose,
    string SelectedText,
    string NormalizedSelectedText,
    string SelectedTextHash,
    string PrefixContext,
    string SuffixContext,
    int StartOffset,
    int EndOffset,
    string CanonicalContentHash,
    string? HtmlSelectorHint = null);
