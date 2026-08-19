using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Tenancy entities.
/// </summary>
public class TenancyRepository(DraftViewDbContext db) : ITenancyRepository
{
    /// <summary>
    /// Returns the Tenancy with the given id, or null if not found.
    /// </summary>
    public Task<Tenancy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Tenancies.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <summary>
    /// Returns the Tenancy owned by the given Account, or null if the account has no tenancy.
    /// </summary>
    public Task<Tenancy?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.Tenancies.FirstOrDefaultAsync(t => t.OwnerAccountId == ownerAccountId && !t.IsSoftDeleted, ct);

    /// <summary>
    /// Adds a new Tenancy to the context for persistence on the next SaveChanges.
    /// </summary>
    public async Task AddAsync(Tenancy tenancy, CancellationToken ct = default) =>
        await db.Tenancies.AddAsync(tenancy, ct);
}
