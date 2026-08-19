using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for ManualChapter. All queries are author-scoped.
/// </summary>
public interface IManualChapterRepository
{
    /// <summary>Returns all chapters for the given project, ordered by SortOrder ascending.</summary>
    Task<IReadOnlyList<ManualChapter>> GetByProjectAsync(Guid projectId, Guid authorId, CancellationToken ct = default);

    /// <summary>Returns the chapter with the given id, scoped to the author, or null if not found.</summary>
    Task<ManualChapter?> GetByIdAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

    /// <summary>Adds a new ManualChapter.</summary>
    Task AddAsync(ManualChapter chapter, CancellationToken ct = default);

    /// <summary>Persists in-place changes to an existing ManualChapter (content, title, sort order).</summary>
    Task UpdateAsync(ManualChapter chapter, CancellationToken ct = default);

    /// <summary>Hard-deletes the chapter with the given id.</summary>
    Task DeleteAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

    /// <summary>Returns the count of chapters in the given project.</summary>
    Task<int> CountByProjectAsync(Guid projectId, Guid authorId, CancellationToken ct = default);
}
