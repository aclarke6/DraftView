using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IScrivenerProjectRepository
{
    Task<ScrivenerProject?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ScrivenerProject>> GetAllAsync(CancellationToken ct = default);
    Task<ScrivenerProject?> GetReaderActiveProjectAsync(CancellationToken ct = default);
    Task<ScrivenerProject?> GetSoftDeletedByScrivenerRootUuidAsync(string uuid, CancellationToken ct = default);
    Task AddAsync(ScrivenerProject project, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent successful syncs (project + timestamp), newest first.
    /// Used by the dashboard notifications panel.
    /// Only returns projects where <see cref="ScrivenerProject.LastSyncedAt"/> is non-null
    /// and <see cref="ScrivenerProject.IsSoftDeleted"/> is false.
    /// </summary>
    Task<IReadOnlyList<(ScrivenerProject Project, DateTime SyncedAt)>> GetRecentlySyncedAsync(
        int take,
        CancellationToken ct = default);
}
