using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for TenancyMembership entities.
/// TenancyMembership is the Author-Tenancy 1:1 link only.
/// Reader access (including authors reading other authors' projects) is managed
/// at the project level via ReaderAccess, not via TenancyMembership.
/// All queries are explicitly tenancy-scoped to prevent cross-tenant data leakage.
/// </summary>
public interface ITenancyMembershipRepository
{
    Task<TenancyMembership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenancyMembership?> GetByTenancyAndAccountAsync(Guid tenancyId, Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<TenancyMembership>> GetByTenancyIdAsync(Guid tenancyId, CancellationToken ct = default);
    Task<IReadOnlyList<TenancyMembership>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task AddAsync(TenancyMembership membership, CancellationToken ct = default);
}
