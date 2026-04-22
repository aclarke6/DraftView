using DraftView.Domain.Exceptions;

namespace DraftView.Domain.ValueObjects;

/// <summary>
/// Immutable capture of the original text and selector data for a passage anchor.
/// </summary>
public sealed class PassageAnchorSnapshot
{
    public string SelectedText { get; private set; } = default!;
    public string NormalizedSelectedText { get; private set; } = default!;
    public string SelectedTextHash { get; private set; } = default!;
    public string PrefixContext { get; private set; } = string.Empty;
    public string SuffixContext { get; private set; } = string.Empty;
    public int StartOffset { get; private set; }
    public int EndOffset { get; private set; }
    public string CanonicalContentHash { get; private set; } = default!;
    public string? HtmlSelectorHint { get; private set; }

    private PassageAnchorSnapshot() { }

    /// <summary>
    /// Creates an immutable original anchor snapshot.
    /// </summary>
    public static PassageAnchorSnapshot Create(
        string selectedText,
        string normalizedSelectedText,
        string selectedTextHash,
        string prefixContext,
        string suffixContext,
        int startOffset,
        int endOffset,
        string canonicalContentHash,
        string? htmlSelectorHint = null)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
            throw new InvariantViolationException("I-ANCHOR-TEXT",
                "Selected text must not be null or whitespace.");

        if (string.IsNullOrWhiteSpace(normalizedSelectedText))
            throw new InvariantViolationException("I-ANCHOR-NORMALIZED",
                "Normalized selected text must not be null or whitespace.");

        if (string.IsNullOrWhiteSpace(selectedTextHash))
            throw new InvariantViolationException("I-ANCHOR-HASH",
                "Selected text hash must not be null or whitespace.");

        if (string.IsNullOrWhiteSpace(canonicalContentHash))
            throw new InvariantViolationException("I-ANCHOR-CONTENT-HASH",
                "Canonical content hash must not be null or whitespace.");

        if (startOffset < 0 || endOffset <= startOffset)
            throw new InvariantViolationException("I-ANCHOR-OFFSET",
                "Anchor end offset must be greater than start offset.");

        return new PassageAnchorSnapshot
        {
            SelectedText = selectedText,
            NormalizedSelectedText = normalizedSelectedText,
            SelectedTextHash = selectedTextHash,
            PrefixContext = prefixContext,
            SuffixContext = suffixContext,
            StartOffset = startOffset,
            EndOffset = endOffset,
            CanonicalContentHash = canonicalContentHash,
            HtmlSelectorHint = htmlSelectorHint
        };
    }
}
