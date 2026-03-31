using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface ISectionRepository
{
    Task<Section?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Section?> GetByScrivenerUuidAsync(Guid projectId, string uuid, CancellationToken ct = default);
    Task<IReadOnlyList<Section>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Section>> GetPublishedByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Section>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<IReadOnlyList<Section>> GetAllDescendantsAsync(Guid parentId, CancellationToken ct = default);
    Task AddAsync(Section section, CancellationToken ct = default);
}
