using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Diff;

/// <summary>
/// Represents a single paragraph in a diff result.
/// Carries the paragraph text and its classification relative to the comparison.
/// </summary>
public sealed class ParagraphDiffResult
{
    /// <summary>The paragraph content as plain text (HTML tags stripped).</summary>
    public string Text { get; }

    /// <summary>The raw paragraph HTML from the source version.</summary>
    public string Html { get; }

    /// <summary>Whether this paragraph was added, removed, or unchanged.</summary>
    public DiffResultType Type { get; }

    public ParagraphDiffResult(string text, string html, DiffResultType type)
    {
        Text = text;
        Html = html;
        Type = type;
    }
}
