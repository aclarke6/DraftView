using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Account entities.
/// </summary>
public class AccountRepository(DraftViewDbContext db) : IAccountRepository
{
    /// <summary>
    /// Returns the Account with the given id, or null if not found.
    /// </summary>
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// Returns the Account matching the given email lookup HMAC, or null if not found.
    /// Used for login and invitation-acceptance lookups without decrypting all emails.
    /// </summary>
    public Task<Account?> GetByEmailLookupHmacAsync(string emailLookupHmac, CancellationToken ct = default) =>
        db.Accounts.FirstOrDefaultAsync(a => a.EmailLookupHmac == emailLookupHmac, ct);

    /// <summary>
    /// Returns true if an Account with the given email lookup HMAC already exists.
    /// </summary>
    public Task<bool> EmailExistsAsync(string emailLookupHmac, CancellationToken ct = default) =>
        db.Accounts.AnyAsync(a => a.EmailLookupHmac == emailLookupHmac, ct);

    /// <summary>
    /// Adds a new Account to the context for persistence on the next SaveChanges.
    /// </summary>
    public async Task AddAsync(Account account, CancellationToken ct = default) =>
        await db.Accounts.AddAsync(account, ct);
}
