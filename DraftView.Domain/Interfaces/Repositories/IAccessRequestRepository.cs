using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IAccessRequestRepository
{
    Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AccessRequest>> GetPendingByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<AccessRequest>> GetVisibleByReaderIdAsync(Guid readerId, DateTime today, CancellationToken ct = default);
    Task<int> GetPendingCountByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(AccessRequest request, CancellationToken ct = default);
    Task SaveAsync(AccessRequest request, CancellationToken ct = default);
    Task BulkDeclineByProjectAsync(Guid projectId, DateTime respondedAt, CancellationToken ct = default);
    Task MarkDeclinedAsSeenAsync(Guid readerId, DateTime now, CancellationToken ct = default);
}
