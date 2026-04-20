using DraftView.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class UserRepository(
    DraftViewDbContext db,
    IUserEmailEncryptionService emailEncryptionService,
    IUserEmailLookupHmacService emailLookupHmacService) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        HydrateEmail(await db.AppUsers.FindAsync([id], ct));

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        HydrateEmail(await db.AppUsers.FirstOrDefaultAsync(
            u => u.EmailLookupHmac == ComputeLookupHmac(email), ct));

    public async Task<User?> GetAuthorAsync(CancellationToken ct = default) =>
        HydrateEmail(await db.AppUsers.FirstOrDefaultAsync(u => u.Role == Role.Author, ct));

    public async Task<IReadOnlyList<User>> GetAllBetaReadersAsync(CancellationToken ct = default) =>
        HydrateEmails(await db.AppUsers
            .Where(u => u.Role == Role.BetaReader && !u.IsSoftDeleted)
            .ToListAsync(ct));

    public async Task<int> CountActiveBetaReadersAsync(CancellationToken ct = default) =>
        await db.AppUsers.CountAsync(u =>
            u.Role == Role.BetaReader &&
            u.IsActive &&
            !u.IsSoftDeleted, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.AppUsers.AddAsync(user, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        await db.AppUsers.AnyAsync(
            u => u.EmailLookupHmac == ComputeLookupHmac(email), ct);

    private string ComputeLookupHmac(string email) =>
        emailLookupHmacService.Compute(DraftViewDbContext.NormalizeEmail(email));

    private User? HydrateEmail(User? user)
    {
        if (user is null)
            return null;

        if (string.IsNullOrWhiteSpace(user.Email) && !string.IsNullOrWhiteSpace(user.EmailCiphertext))
            user.LoadEmailForRuntime(emailEncryptionService.Decrypt(user.EmailCiphertext));

        return user;
    }

    private IReadOnlyList<User> HydrateEmails(IReadOnlyList<User> users)
    {
        foreach (var user in users)
            HydrateEmail(user);

        return users;
    }
}
