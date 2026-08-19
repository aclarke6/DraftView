using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class ReaderAccessRepository(DraftViewDbContext db) : IReaderAccessRepository
{
    public Task<IReadOnlyList<ReaderAccess>> GetByReaderIdAsync(
        Guid readerId, CancellationToken ct = default) =>
        db.ReaderAccess
            .Where(r => r.ReaderId == readerId && r.RevokedAt == null)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ReaderAccess>)t.Result, ct);

    public Task<IReadOnlyList<ReaderAccess>> GetByProjectIdAsync(
        Guid projectId, CancellationToken ct = default) =>
        db.ReaderAccess
            .Where(r => r.ProjectId == projectId)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<ReaderAccess>)t.Result, ct);

    public Task<ReaderAccess?> GetByReaderAndProjectAsync(
        Guid readerId, Guid projectId, CancellationToken ct = default) =>
        db.ReaderAccess
            .FirstOrDefaultAsync(r => r.ReaderId == readerId && r.ProjectId == projectId, ct);

    public async Task AddAsync(ReaderAccess access, CancellationToken ct = default) =>
        await db.ReaderAccess.AddAsync(access, ct);

    public async Task RevokeAllForReaderAsync(
    Guid readerId, Guid authorId, CancellationToken ct = default)
    {
        var records = await db.ReaderAccess
            .Where(r => r.ReaderId == readerId
                     && r.AuthorId == authorId
                     && r.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var record in records)
            record.Revoke();
    }

    public Task<int> CountActiveReadersForAuthorAsync(Guid authorId, CancellationToken ct = default) =>
        db.ReaderAccess.CountAsync(r => r.AuthorId == authorId && r.RevokedAt == null, ct);
}
