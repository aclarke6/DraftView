using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persists passage anchors and retrieves them for application orchestration.
/// </summary>
public interface IPassageAnchorRepository
{
    Task<PassageAnchor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PassageAnchor>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default);
    Task AddAsync(PassageAnchor anchor, CancellationToken ct = default);
}
