namespace DraftView.Domain.Contracts;

/// <summary>
/// Immutable original metadata returned for a passage anchor.
/// </summary>
public sealed record PassageAnchorSnapshotDto(
    string SelectedText,
    string NormalizedSelectedText,
    string SelectedTextHash,
    string PrefixContext,
    string SuffixContext,
    int StartOffset,
    int EndOffset,
    string CanonicalContentHash,
    string? HtmlSelectorHint);
