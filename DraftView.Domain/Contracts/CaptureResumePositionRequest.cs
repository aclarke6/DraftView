namespace DraftView.Domain.Contracts;

/// <summary>
/// Capture payload for saving a reader's latest resume position as a passage anchor.
/// </summary>
public sealed record CaptureResumePositionRequest(
    Guid SectionId,
    Guid? OriginalSectionVersionId,
    string SelectedText,
    string NormalizedSelectedText,
    string SelectedTextHash,
    string PrefixContext,
    string SuffixContext,
    int StartOffset,
    int EndOffset,
    string CanonicalContentHash,
    string? HtmlSelectorHint = null);
