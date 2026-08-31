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

    /// <summary>Words inserted in this paragraph relative to the previous version. Zero for Unchanged/Removed.</summary>
    public int WordsAdded { get; }

    /// <summary>Words deleted in this paragraph relative to the previous version. Zero for Unchanged/Added.</summary>
    public int WordsRemoved { get; }

    /// <summary>Total words in the current version of this paragraph. Zero for Removed paragraphs.</summary>
    public int TotalWords { get; }

    public ParagraphDiffResult(
        string text,
        string html,
        DiffResultType type,
        int wordsAdded = 0,
        int wordsRemoved = 0,
        int totalWords = 0)
    {
        Text         = text;
        Html         = html;
        Type         = type;
        WordsAdded   = wordsAdded;
        WordsRemoved = wordsRemoved;
        TotalWords   = totalWords;
    }
}
