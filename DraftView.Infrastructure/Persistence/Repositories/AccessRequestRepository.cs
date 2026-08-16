using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class AccessRequestRepository(DraftViewDbContext db) : IAccessRequestRepository
{
    public async Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.AccessRequests.FindAsync([id], ct);

    public async Task<IReadOnlyList<AccessRequest>> GetPendingByProjectIdAsync(
        Guid projectId, CancellationToken ct = default) =>
        await db.AccessRequests
            .Where(r => r.ProjectId == projectId && r.Status == AccessRequestStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccessRequest>> GetVisibleByReaderIdAsync(
        Guid readerId, DateTime today, CancellationToken ct = default) =>
        await db.AccessRequests
            .Where(r => r.ReaderId == readerId && (
                r.Status == AccessRequestStatus.Pending ||
                (r.Status == AccessRequestStatus.Declined &&
                    (r.SeenByReaderAt == null || r.SeenByReaderAt.Value.Date >= today.Date))))
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

    public async Task<int> GetPendingCountByProjectIdAsync(
        Guid projectId, CancellationToken ct = default) =>
        await db.AccessRequests
            .CountAsync(r => r.ProjectId == projectId && r.Status == AccessRequestStatus.Pending, ct);

    public async Task AddAsync(AccessRequest request, CancellationToken ct = default) =>
        await db.AccessRequests.AddAsync(request, ct);

    public Task SaveAsync(AccessRequest request, CancellationToken ct = default)
    {
        db.AccessRequests.Update(request);
        return Task.CompletedTask;
    }

    public async Task BulkDeclineByProjectAsync(
        Guid projectId, DateTime respondedAt, CancellationToken ct = default)
    {
        var pending = await db.AccessRequests
            .Where(r => r.ProjectId == projectId && r.Status == AccessRequestStatus.Pending)
            .ToListAsync(ct);

        foreach (var r in pending)
            r.Decline();
    }

    public async Task MarkDeclinedAsSeenAsync(
        Guid readerId, DateTime now, CancellationToken ct = default)
    {
        var unseen = await db.AccessRequests
            .Where(r => r.ReaderId == readerId &&
                        r.Status == AccessRequestStatus.Declined &&
                        r.SeenByReaderAt == null)
            .ToListAsync(ct);

        foreach (var r in unseen)
            r.MarkSeenByReader();
    }
}
