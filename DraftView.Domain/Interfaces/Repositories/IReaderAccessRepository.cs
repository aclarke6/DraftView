using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IReaderAccessRepository
{
    /// <summary>Gets all active access records for a reader.</summary>
    Task<IReadOnlyList<ReaderAccess>> GetByReaderIdAsync(Guid readerId, CancellationToken ct = default);

    /// <summary>Gets all access records (active and revoked) for a project.</summary>
    Task<IReadOnlyList<ReaderAccess>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Gets the access record for a specific reader/project pair, or null.</summary>
    Task<ReaderAccess?> GetByReaderAndProjectAsync(Guid readerId, Guid projectId, CancellationToken ct = default);

    /// <summary>Adds a new ReaderAccess record.</summary>
    Task AddAsync(ReaderAccess access, CancellationToken ct = default);
}
