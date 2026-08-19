namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Parses a chapter file stream into stored markdown content.
/// One implementation per supported file extension; selection is polymorphic.
/// </summary>
public interface IChapterFileParser
{
    /// <summary>The file extension this parser handles, including the leading dot (e.g. ".txt").</summary>
    string SupportedExtension { get; }

    /// <summary>
    /// Reads the stream and returns the chapter content as a markdown string.
    /// For .txt files content is returned as-is; for .docx files headings and
    /// character emphasis are mapped to markdown equivalents.
    /// </summary>
    Task<string> ParseAsync(Stream content, CancellationToken ct = default);
}
