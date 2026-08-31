using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Computes the diff between what a reader last read and the current version.
/// Coordinates version lookup, HTML diff computation, classification, and
/// reader preference filtering (cooldown and ReadingStyle threshold).
/// </summary>
public class SectionDiffService(
    ISectionVersionRepository versionRepo,
    IHtmlDiffService htmlDiffService,
    IChangeClassificationService classificationService) : ISectionDiffService
{
    private const int SystemCooldownFloorHours = 1;

    /// <summary>
    /// Legacy overload — no cooldown or threshold filtering.
    /// Uses BetaReader threshold (all classifications pass). Delegates to the full overload.
    /// </summary>
    public Task<SectionDiffResult?> GetDiffForReaderAsync(
        Guid sectionId,
        int? lastReadVersionNumber,
        CancellationToken ct = default)
        => GetDiffForReaderAsync(
            sectionId, lastReadVersionNumber,
            lastMarkedReadAt: null,
            diffCooldownHours: SystemCooldownFloorHours,
            readingStyle: ReadingStyle.BetaReader,
            ct);

    /// <summary>
    /// Returns the diff filtered by the reader's cooldown and ReadingStyle threshold.
    /// Returns null when: no version exists, cooldown is active, or classification is below threshold.
    /// </summary>
    public async Task<SectionDiffResult?> GetDiffForReaderAsync(
        Guid sectionId,
        int? lastReadVersionNumber,
        DateTimeOffset? lastMarkedReadAt,
        int diffCooldownHours,
        ReadingStyle readingStyle,
        CancellationToken ct = default)
    {
        if (IsCooldownActive(lastMarkedReadAt, diffCooldownHours))
            return null;

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

        var diffParagraphs = htmlDiffService.Compute(fromVersion.HtmlContent, latestVersion.HtmlContent);
        var classification  = classificationService.Classify(diffParagraphs);

        if (!MeetsThreshold(classification, readingStyle))
            return null;

        return new SectionDiffResult
        {
            FromVersionNumber    = lastReadVersionNumber.Value,
            CurrentVersionNumber = latestVersion.VersionNumber,
            HasChanges           = true,
            Paragraphs           = diffParagraphs,
            Classification       = classification
        };
    }

    private static bool IsCooldownActive(DateTimeOffset? lastMarkedReadAt, int diffCooldownHours)
    {
        if (lastMarkedReadAt is null)
            return false;

        var effectiveCooldown = Math.Max(SystemCooldownFloorHours, diffCooldownHours);
        return DateTimeOffset.UtcNow < lastMarkedReadAt.Value.AddHours(effectiveCooldown);
    }

    private static bool MeetsThreshold(ChangeClassification? classification, ReadingStyle readingStyle)
    {
        if (classification is null)
            return false;

        var minimum = readingStyle switch
        {
            ReadingStyle.BetaReader    => ChangeClassification.Trivial,
            ReadingStyle.StoryReader   => ChangeClassification.Polish,
            ReadingStyle.AlphaReader   => ChangeClassification.Revision,
            ReadingStyle.StructureOnly => ChangeClassification.Rewrite,
            _                          => ChangeClassification.Polish
        };

        return classification >= minimum;
    }

    private static SectionDiffResult CreateNoChangesResult(int? fromVersionNumber, int currentVersionNumber)
        => new()
        {
            FromVersionNumber    = fromVersionNumber,
            CurrentVersionNumber = currentVersionNumber,
            HasChanges           = false,
            Paragraphs           = Array.Empty<Domain.Diff.ParagraphDiffResult>()
        };

    private static SectionDiffResult CreateHasChangesResultWithoutDiff(int fromVersionNumber, int currentVersionNumber)
        => new()
        {
            FromVersionNumber    = fromVersionNumber,
            CurrentVersionNumber = currentVersionNumber,
            HasChanges           = true,
            Paragraphs           = Array.Empty<Domain.Diff.ParagraphDiffResult>()
        };
}
