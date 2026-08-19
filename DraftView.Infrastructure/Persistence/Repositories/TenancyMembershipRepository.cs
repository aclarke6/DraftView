using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for TenancyMembership entities.
/// TenancyMembership is the Author-Tenancy 1:1 link only; reader access is managed
/// at the project level via ReaderAccess. All queries are tenancy-scoped.
/// </summary>
public class TenancyMembershipRepository(DraftViewDbContext db) : ITenancyMembershipRepository
{
    public Task<TenancyMembership?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.TenancyMemberships.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<TenancyMembership?> GetByTenancyAndAccountAsync(
        Guid tenancyId, Guid accountId, CancellationToken ct = default) =>
        db.TenancyMemberships.FirstOrDefaultAsync(
            m => m.TenancyId == tenancyId && m.AccountId == accountId && !m.IsSoftDeleted, ct);

    public async Task<IReadOnlyList<TenancyMembership>> GetByTenancyIdAsync(
        Guid tenancyId, CancellationToken ct = default) =>
        await db.TenancyMemberships
            .Where(m => m.TenancyId == tenancyId && !m.IsSoftDeleted)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenancyMembership>> GetByAccountIdAsync(
        Guid accountId, CancellationToken ct = default) =>
        await db.TenancyMemberships
            .Where(m => m.AccountId == accountId && !m.IsSoftDeleted)
            .ToListAsync(ct);

    public async Task AddAsync(TenancyMembership membership, CancellationToken ct = default) =>
        await db.TenancyMemberships.AddAsync(membership, ct);
}
