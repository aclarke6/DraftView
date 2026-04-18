using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Computes the diff between what a reader last read and the current version.
/// Coordinates version lookup and HTML diff computation.
/// </summary>
public class SectionDiffService(
    ISectionVersionRepository versionRepo,
    IHtmlDiffService htmlDiffService) : ISectionDiffService
{
    /// <summary>
    /// Returns the diff for a section from the reader's last read version
    /// to the current latest version. Returns null if no current version exists.
    /// Returns a result with HasChanges = false if the reader is on the latest version.
    /// </summary>
    public async Task<SectionDiffResult?> GetDiffForReaderAsync(
        Guid sectionId,
        int? lastReadVersionNumber,
        CancellationToken ct = default)
    {
        var latestVersion = await versionRepo.GetLatestAsync(sectionId, ct);

        if (latestVersion is null)
            return null;

        if (lastReadVersionNumber is null)
            return CreateNoChangesResult(null, latestVersion.VersionNumber);

        if (lastReadVersionNumber == latestVersion.VersionNumber)
            return CreateNoChangesResult(lastReadVersionNumber, latestVersion.VersionNumber);

        var allVersions = await versionRepo.GetAllBySectionIdAsync(sectionId, ct);
        var fromVersion = allVersions.FirstOrDefault(v => v.VersionNumber == lastReadVersionNumber);

        if (fromVersion is null)
            return CreateHasChangesResultWithoutDiff(lastReadVersionNumber.Value, latestVersion.VersionNumber);

        var diffParagraphs = htmlDiffService.Compute(
            fromVersion.HtmlContent,
            latestVersion.HtmlContent);

        return CreateHasChangesResultWithDiff(
            lastReadVersionNumber.Value,
            latestVersion.VersionNumber,
            diffParagraphs);
    }

    private static SectionDiffResult CreateNoChangesResult(int? fromVersionNumber, int currentVersionNumber)
    {
        return new SectionDiffResult
        {
            FromVersionNumber = fromVersionNumber,
            CurrentVersionNumber = currentVersionNumber,
            HasChanges = false,
            Paragraphs = Array.Empty<Domain.Diff.ParagraphDiffResult>()
        };
    }

    private static SectionDiffResult CreateHasChangesResultWithoutDiff(int fromVersionNumber, int currentVersionNumber)
    {
        return new SectionDiffResult
        {
            FromVersionNumber = fromVersionNumber,
            CurrentVersionNumber = currentVersionNumber,
            HasChanges = true,
            Paragraphs = Array.Empty<Domain.Diff.ParagraphDiffResult>()
        };
    }

    private static SectionDiffResult CreateHasChangesResultWithDiff(
        int fromVersionNumber,
        int currentVersionNumber,
        IReadOnlyList<Domain.Diff.ParagraphDiffResult> paragraphs)
    {
        return new SectionDiffResult
        {
            FromVersionNumber = fromVersionNumber,
            CurrentVersionNumber = currentVersionNumber,
            HasChanges = true,
            Paragraphs = paragraphs
        };
    }
}
