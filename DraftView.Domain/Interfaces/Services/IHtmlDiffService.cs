using DraftView.Domain.Diff;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Computes a paragraph-level diff between two HTML content strings.
/// Source-agnostic — makes no distinction between sync and import content.
/// </summary>
public interface IHtmlDiffService
{
    /// <summary>
    /// Computes a paragraph-level diff between the from and to HTML strings.
    /// Returns a list of ParagraphDiffResult ordered as they appear in the
    /// combined sequence (removed paragraphs from `from`, added paragraphs
    /// from `to`, unchanged paragraphs preserved in position).
    /// </summary>
    IReadOnlyList<ParagraphDiffResult> Compute(string? from, string? to);
}
