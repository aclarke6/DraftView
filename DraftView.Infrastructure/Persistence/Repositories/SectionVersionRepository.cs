using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class SectionVersionRepository(DraftViewDbContext db) : ISectionVersionRepository
{
    public async Task<int> GetMaxVersionNumberAsync(Guid sectionId, CancellationToken ct = default)
    {
        var maxVersion = await db.SectionVersions
            .Where(v => v.SectionId == sectionId)
            .MaxAsync(v => (int?)v.VersionNumber, ct);

        return maxVersion ?? 0;
    }

    public async Task<SectionVersion?> GetLatestAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.SectionVersions
            .Where(v => v.SectionId == sectionId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<SectionVersion>> GetAllBySectionIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.SectionVersions
            .Where(v => v.SectionId == sectionId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(ct);

    public async Task AddAsync(SectionVersion version, CancellationToken ct = default) =>
        await db.SectionVersions.AddAsync(version, ct);
}
