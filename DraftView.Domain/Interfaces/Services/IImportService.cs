namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates the manual file import flow.
/// Resolves the correct IImportProvider by file extension,
/// converts the file to HTML, and writes the result to Section.HtmlContent.
/// Import never creates SectionVersion records.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Converts the file stream to HTML via the appropriate IImportProvider
    /// and writes the result to Section.HtmlContent.
    /// Updates ContentHash. Sets ContentChangedSincePublish if a SectionVersion exists.
    /// Throws UnsupportedFileTypeException if no provider handles the file extension.
    /// </summary>
    Task ImportAsync(
        Guid projectId,
        Guid sectionId,
        Stream fileStream,
        string fileName,
        Guid authorId,
        CancellationToken cancellationToken = default);
}
