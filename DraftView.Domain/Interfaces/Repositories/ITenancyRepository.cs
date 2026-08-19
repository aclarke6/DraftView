using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for Tenancy entities.
/// </summary>
public interface ITenancyRepository
{
    Task<Tenancy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenancy?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default);
    Task AddAsync(Tenancy tenancy, CancellationToken ct = default);
}
