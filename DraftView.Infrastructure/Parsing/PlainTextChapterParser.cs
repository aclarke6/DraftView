using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Parsing;

/// <summary>
/// Parses .txt chapter files. Content is stored as-is; authors may use markdown syntax directly.
/// </summary>
public class PlainTextChapterParser : IChapterFileParser
{
    public string SupportedExtension => ".txt";

    /// <summary>Reads the stream as UTF-8 text and returns it unchanged.</summary>
    public async Task<string> ParseAsync(Stream content, CancellationToken ct = default)
    {
        using var reader = new StreamReader(content, System.Text.Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }
}
