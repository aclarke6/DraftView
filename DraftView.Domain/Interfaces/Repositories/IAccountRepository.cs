using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

/// <summary>
/// Persistence contract for Account entities.
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Account?> GetByEmailLookupHmacAsync(string emailLookupHmac, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string emailLookupHmac, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
}
