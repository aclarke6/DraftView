using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class ReaderSnapshotRepository(DraftViewDbContext db) : IReaderSnapshotRepository
{
    public Task<ReaderSnapshot?> GetAsync(Guid sectionId, Guid userId, CancellationToken ct = default) =>
        db.ReaderSnapshots
            .FirstOrDefaultAsync(s => s.SectionId == sectionId && s.UserId == userId, ct);

    public async Task UpsertAsync(ReaderSnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await db.ReaderSnapshots
            .FirstOrDefaultAsync(s => s.SectionId == snapshot.SectionId && s.UserId == snapshot.UserId, ct);

        if (existing is not null)
            db.ReaderSnapshots.Remove(existing);

        await db.ReaderSnapshots.AddAsync(snapshot, ct);
    }

    public async Task<IReadOnlyList<ReaderSnapshot>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.ReaderSnapshots
            .Where(s => s.SectionId == sectionId)
            .ToListAsync(ct);
}
