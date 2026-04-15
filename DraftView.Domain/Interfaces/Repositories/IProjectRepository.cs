using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default);
    Task<Project?> GetReaderActiveProjectAsync(CancellationToken ct = default);
    Task<Project?> GetSoftDeletedBySyncRootIdAsync(string uuid, CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
}
