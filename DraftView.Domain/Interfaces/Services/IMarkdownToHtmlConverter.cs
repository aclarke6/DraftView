namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Converts stored markdown content to HTML for reader delivery.
/// Applied at publish time; the reader surface always reads HTML from SectionVersion.
/// </summary>
public interface IMarkdownToHtmlConverter
{
    /// <summary>
    /// Converts markdown text to an HTML fragment.
    /// Supports: # ## ### headings, **bold**, *italic*, and blank-line-separated paragraphs.
    /// </summary>
    string Convert(string markdown);
}
