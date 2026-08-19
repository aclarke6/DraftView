using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for TenancyMembership entities.
/// All queries are explicitly tenancy-scoped to prevent cross-tenant data leakage.
/// </summary>
public class TenancyMembershipRepository(DraftViewDbContext db) : ITenancyMembershipRepository
{
    /// <summary>
    /// Returns the membership with the given id, or null if not found.
    /// </summary>
    public Task<TenancyMembership?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TenancyMemberships.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <summary>
    /// Returns the active membership for a specific Account within a specific Tenancy, or null.
    /// </summary>
    public Task<TenancyMembership?> GetByTenancyAndAccountAsync(
        Guid tenancyId, Guid accountId, CancellationToken ct = default) =>
        db.TenancyMemberships.FirstOrDefaultAsync(
            m => m.TenancyId == tenancyId && m.AccountId == accountId && !m.IsSoftDeleted, ct);

    /// <summary>
    /// Returns all active memberships for the given Tenancy.
    /// </summary>
    public async Task<IReadOnlyList<TenancyMembership>> GetByTenancyIdAsync(
        Guid tenancyId, CancellationToken ct = default) =>
        await db.TenancyMemberships
            .Where(m => m.TenancyId == tenancyId && !m.IsSoftDeleted)
            .ToListAsync(ct);

    /// <summary>
    /// Returns all active memberships held by the given Account across all tenancies.
    /// </summary>
    public async Task<IReadOnlyList<TenancyMembership>> GetByAccountIdAsync(
        Guid accountId, CancellationToken ct = default) =>
        await db.TenancyMemberships
            .Where(m => m.AccountId == accountId && !m.IsSoftDeleted)
            .ToListAsync(ct);

    /// <summary>
    /// Returns the count of active BetaReader memberships within the given Tenancy.
    /// Used to enforce MaxBetaReaderCount before adding a new reader.
    /// </summary>
    public Task<int> CountActiveBetaReadersAsync(Guid tenancyId, CancellationToken ct = default) =>
        db.TenancyMemberships.CountAsync(
            m => m.TenancyId == tenancyId &&
                 m.Role == TenancyRole.BetaReader &&
                 !m.IsSoftDeleted,
            ct);

    /// <summary>
    /// Adds a new TenancyMembership to the context for persistence on the next SaveChanges.
    /// </summary>
    public async Task AddAsync(TenancyMembership membership, CancellationToken ct = default) =>
        await db.TenancyMemberships.AddAsync(membership, ct);
}
