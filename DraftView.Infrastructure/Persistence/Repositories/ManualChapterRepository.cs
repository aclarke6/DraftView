using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for ManualChapter. All queries are author-scoped.
/// </summary>
public class ManualChapterRepository(DraftViewDbContext db) : IManualChapterRepository
{
    /// <summary>Returns all chapters for the given project ordered by SortOrder ascending.</summary>
    public async Task<IReadOnlyList<ManualChapter>> GetByProjectAsync(
        Guid projectId, Guid authorId, CancellationToken ct = default) =>
        await db.ManualChapters
            .Where(c => c.ProjectId == projectId && c.AuthorId == authorId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

    /// <summary>Returns the chapter with the given id scoped to the author, or null.</summary>
    public async Task<ManualChapter?> GetByIdAsync(
        Guid chapterId, Guid authorId, CancellationToken ct = default) =>
        await db.ManualChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.AuthorId == authorId, ct);

    /// <summary>Adds a new ManualChapter.</summary>
    public async Task AddAsync(ManualChapter chapter, CancellationToken ct = default) =>
        await db.ManualChapters.AddAsync(chapter, ct);

    /// <summary>Marks an existing ManualChapter as modified so EF tracks in-place changes.</summary>
    public Task UpdateAsync(ManualChapter chapter, CancellationToken ct = default)
    {
        db.ManualChapters.Update(chapter);
        return Task.CompletedTask;
    }

    /// <summary>Hard-deletes the chapter with the given id, scoped to the author.</summary>
    public async Task DeleteAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        var chapter = await db.ManualChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.AuthorId == authorId, ct);

        if (chapter is not null)
            db.ManualChapters.Remove(chapter);
    }

    /// <summary>Returns the count of chapters in the given project scoped to the author.</summary>
    public Task<int> CountByProjectAsync(Guid projectId, Guid authorId, CancellationToken ct = default) =>
        db.ManualChapters
            .CountAsync(c => c.ProjectId == projectId && c.AuthorId == authorId, ct);
}
