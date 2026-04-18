using DraftView.Domain.Contracts;

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
    /// </summary>
    Task<SectionDiffResult?> GetDiffForReaderAsync(
        Guid sectionId,
        int? lastReadVersionNumber,
        CancellationToken ct = default);
}
