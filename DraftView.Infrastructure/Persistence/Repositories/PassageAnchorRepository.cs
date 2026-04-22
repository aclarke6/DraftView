using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for passage anchors.
/// </summary>
public class PassageAnchorRepository(DraftViewDbContext db) : IPassageAnchorRepository
{
    public async Task<PassageAnchor?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.PassageAnchors.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<PassageAnchor>> GetBySectionIdAsync(
        Guid sectionId,
        CancellationToken ct = default) =>
        await db.PassageAnchors
            .Where(a => a.SectionId == sectionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(PassageAnchor anchor, CancellationToken ct = default) =>
        await db.PassageAnchors.AddAsync(anchor, ct);
}
