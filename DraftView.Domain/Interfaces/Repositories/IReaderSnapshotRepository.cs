using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Repository contract for ReaderSnapshot persistence.
/// One snapshot per (SectionId, UserId) pair — upserted when a reader's read state becomes true.
/// </summary>
public interface IReaderSnapshotRepository
{
    /// <summary>Returns the snapshot for a specific reader and scene, or null if none exists.</summary>
    Task<ReaderSnapshot?> GetAsync(Guid sectionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Inserts or replaces the snapshot for a (SectionId, UserId) pair.
    /// The snapshot records what the reader last read.
    /// </summary>
    Task UpsertAsync(ReaderSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Returns all reader snapshots for a scene. Used by the author readership view.</summary>
    Task<IReadOnlyList<ReaderSnapshot>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default);
}
