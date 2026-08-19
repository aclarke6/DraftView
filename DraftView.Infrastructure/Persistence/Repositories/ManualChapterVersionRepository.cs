using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for ManualChapterVersion. Versions are immutable; ClearForChapterAsync is the only removal path.
/// </summary>
public class ManualChapterVersionRepository(DraftViewDbContext db) : IManualChapterVersionRepository
{
    /// <summary>Returns all versions for the given chapter, ordered by VersionNumber descending.</summary>
    public async Task<IReadOnlyList<ManualChapterVersion>> GetByChapterAsync(
        Guid chapterId, CancellationToken ct = default) =>
        await db.ManualChapterVersions
            .Where(v => v.ManualChapterId == chapterId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

    /// <summary>Adds a new ManualChapterVersion snapshot.</summary>
    public async Task AddAsync(ManualChapterVersion version, CancellationToken ct = default) =>
        await db.ManualChapterVersions.AddAsync(version, ct);

    /// <summary>
    /// Hard-deletes all version records for the given chapter.
    /// Does not affect the live ManualChapter content.
    /// </summary>
    public async Task ClearForChapterAsync(Guid chapterId, CancellationToken ct = default)
    {
        var versions = await db.ManualChapterVersions
            .Where(v => v.ManualChapterId == chapterId)
            .ToListAsync(ct);

        db.ManualChapterVersions.RemoveRange(versions);
    }

    /// <summary>Returns the next version number (max existing + 1, or 1 if none exist).</summary>
    public async Task<int> GetNextVersionNumberAsync(Guid chapterId, CancellationToken ct = default)
    {
        var max = await db.ManualChapterVersions
            .Where(v => v.ManualChapterId == chapterId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct);

        return (max ?? 0) + 1;
    }
}
