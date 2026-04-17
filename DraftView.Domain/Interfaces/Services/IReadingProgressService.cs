using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IReadingProgressService
{
    Task RecordOpenAsync(Guid sectionId, Guid userId, CancellationToken ct = default);
    Task<bool> IsCaughtUpAsync(Guid userId, Guid projectId, CancellationToken ct = default);
    Task<bool> HasReadSectionAsync(Guid userId, Guid sectionId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadEvent>> GetProgressForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<ReadEvent?> GetLastReadEventAsync(Guid userId, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Updates the LastReadVersionNumber on an existing ReadEvent.
    /// Called when a reader opens a section that has a current SectionVersion.
    /// Does nothing if no ReadEvent exists for the pair.
    /// </summary>
    Task UpdateLastReadVersionAsync(
        Guid sectionId,
        Guid userId,
        int versionNumber,
        CancellationToken ct = default);
}
