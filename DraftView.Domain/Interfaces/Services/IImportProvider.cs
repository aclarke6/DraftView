namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Converts a file stream to HTML for ingestion into a Section's working state.
/// Import providers are conversion-only — they never write to the database.
/// The write to Section.HtmlContent is owned by ImportService.
/// </summary>
public interface IImportProvider
{
    /// <summary>File extension this provider handles, including the dot. E.g. ".rtf"</summary>
    string SupportedExtension { get; }

    /// <summary>Display name for this provider. E.g. "RTF"</summary>
    string ProviderName { get; }

    /// <summary>
    /// Converts the file stream to HTML. Returns the HTML string.
    /// Throws UnsupportedFileTypeException if the file cannot be parsed.
    /// </summary>
    Task<string> ConvertToHtmlAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default);
}
