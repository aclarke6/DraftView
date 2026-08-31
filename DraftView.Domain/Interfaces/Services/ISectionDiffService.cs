using DraftView.Domain.Contracts;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Computes the diff between what a reader last read and the current version.
/// </summary>
public interface ISectionDiffService
{
    /// <summary>
    /// Returns the diff for a section from the reader's last read version
    /// to the current latest version. Returns null if no current version exists.
    /// Returns a result with HasChanges = false if the reader is on the latest version.
    /// Uses BetaReader threshold and no cooldown — for legacy callers.
    /// </summary>
    Task<SectionDiffResult?> GetDiffForReaderAsync(
        Guid sectionId,
        int? lastReadVersionNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the diff filtered by the reader's cooldown and ReadingStyle threshold.
    /// Returns null when: no version exists, cooldown is active, or classification is below threshold.
    /// </summary>
    Task<SectionDiffResult?> GetDiffForReaderAsync(
        Guid sectionId,
        int? lastReadVersionNumber,
        DateTimeOffset? lastMarkedReadAt,
        int diffCooldownHours,
        ReadingStyle readingStyle,
        CancellationToken ct = default);
}
