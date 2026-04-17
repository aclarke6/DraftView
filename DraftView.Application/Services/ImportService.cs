using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using System.Security.Cryptography;
using System.Text;

namespace DraftView.Application.Services;

/// <summary>
/// Orchestrates the manual import pipeline and writes converted HTML into sections.
/// </summary>
public class ImportService(
    ISectionRepository sectionRepository,
    ISectionVersionRepository sectionVersionRepository,
    IUnitOfWork unitOfWork,
    IEnumerable<IImportProvider> importProviders) : IImportService
{
    /// <summary>
    /// Resolves the import provider for the file extension, converts the stream,
    /// and writes the resulting HTML to the target section.
    /// </summary>
    public Task ImportAsync(
        Guid projectId,
        Guid sectionId,
        Stream fileStream,
        string fileName,
        Guid authorId,
        CancellationToken cancellationToken = default)
        => ImportCoreAsync(sectionId, fileName, fileStream, cancellationToken);

    private async Task ImportCoreAsync(
        Guid sectionId,
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        var provider = importProviders.FirstOrDefault(p =>
            string.Equals(p.SupportedExtension, extension, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
            throw new UnsupportedFileTypeException(extension);

        var html = await provider.ConvertToHtmlAsync(fileStream, cancellationToken);
        var section = await sectionRepository.GetByIdAsync(sectionId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)));
        section.UpdateContent(html, hash);

        var latestVersion = await sectionVersionRepository.GetLatestAsync(sectionId, cancellationToken);
        if (latestVersion is not null)
            section.MarkContentChanged();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
