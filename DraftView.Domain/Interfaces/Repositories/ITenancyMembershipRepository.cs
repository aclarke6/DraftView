using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for TenancyMembership entities.
/// All queries are explicitly tenancy-scoped to prevent cross-tenant data leakage.
/// </summary>
public interface ITenancyMembershipRepository
{
    Task<TenancyMembership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenancyMembership?> GetByTenancyAndAccountAsync(Guid tenancyId, Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<TenancyMembership>> GetByTenancyIdAsync(Guid tenancyId, CancellationToken ct = default);
    Task<IReadOnlyList<TenancyMembership>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task<int> CountActiveBetaReadersAsync(Guid tenancyId, CancellationToken ct = default);
    Task AddAsync(TenancyMembership membership, CancellationToken ct = default);
}
