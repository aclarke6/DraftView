namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Resolves the correct IChapterFileParser for a given file extension.
/// Throws UnsupportedFileTypeException for unregistered extensions.
/// </summary>
public interface IChapterFileParserResolver
{
    /// <summary>
    /// Returns the parser registered for the given extension (e.g. ".txt", ".docx").
    /// Extension comparison is case-insensitive.
    /// </summary>
    IChapterFileParser Resolve(string fileExtension);
}
