using DraftView.Domain.Diff;

namespace DraftView.Domain.Contracts;

/// <summary>
/// The result of comparing two versions of a section.
/// Contains the paragraph-level diff and version metadata.
/// </summary>
public sealed class SectionDiffResult
{
    /// <summary>The version number the reader last read. Null if never read.</summary>
    public int? FromVersionNumber { get; init; }

    /// <summary>The current latest version number.</summary>
    public int CurrentVersionNumber { get; init; }

    /// <summary>True when the reader's last read version differs from the current version.</summary>
    public bool HasChanges { get; init; }

    /// <summary>Paragraph-level diff results. Empty when no changes or no prior version.</summary>
    public IReadOnlyList<ParagraphDiffResult> Paragraphs { get; init; }
        = Array.Empty<ParagraphDiffResult>();
}
