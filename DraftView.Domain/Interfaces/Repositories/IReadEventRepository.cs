using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IReadEventRepository
{
    Task<ReadEvent?> GetAsync(Guid sectionId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadEvent>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadEvent>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadEvent>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<bool> HasReadAsync(Guid sectionId, Guid userId, CancellationToken ct = default);
    Task AddAsync(ReadEvent readEvent, CancellationToken ct = default);
}
