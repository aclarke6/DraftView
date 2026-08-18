using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for ManualChapterVersion. Versions are immutable after creation.
/// The only removal path is ClearForChapterAsync (hard delete).
/// </summary>
public interface IManualChapterVersionRepository
{
    /// <summary>Returns all versions for the given chapter, ordered by VersionNumber descending.</summary>
    Task<IReadOnlyList<ManualChapterVersion>> GetByChapterAsync(Guid chapterId, CancellationToken ct = default);

    /// <summary>Adds a new ManualChapterVersion snapshot.</summary>
    Task AddAsync(ManualChapterVersion version, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes all version records for the given chapter.
    /// Does not affect the live ManualChapter content.
    /// </summary>
    Task ClearForChapterAsync(Guid chapterId, CancellationToken ct = default);

    /// <summary>Returns the highest VersionNumber for the given chapter, or 0 if none exist.</summary>
    Task<int> GetNextVersionNumberAsync(Guid chapterId, CancellationToken ct = default);
}
