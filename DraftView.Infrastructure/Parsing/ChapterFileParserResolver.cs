using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Parsing;

/// <summary>
/// Selects the correct IChapterFileParser by file extension.
/// All registered parsers are injected via DI; no switch statements.
/// </summary>
public class ChapterFileParserResolver(IEnumerable<IChapterFileParser> parsers) : IChapterFileParserResolver
{
    /// <summary>
    /// Returns the parser whose SupportedExtension matches the given extension (case-insensitive).
    /// Throws UnsupportedFileTypeException if no parser is registered for the extension.
    /// </summary>
    public IChapterFileParser Resolve(string fileExtension)
    {
        var parser = parsers.FirstOrDefault(p =>
            string.Equals(p.SupportedExtension, fileExtension, StringComparison.OrdinalIgnoreCase));

        if (parser is null)
            throw new UnsupportedFileTypeException(fileExtension);

        return parser;
    }
}
