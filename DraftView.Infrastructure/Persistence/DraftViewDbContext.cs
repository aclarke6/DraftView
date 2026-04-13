using DraftView.Application.Interfaces;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence;

/// <summary>
/// Single DbContext hosting both ASP.NET Core Identity tables (for authentication)
/// and DraftView domain tables (for business logic).
/// IdentityUser handles login only. Domain User handles roles, comments, invitations.
/// The two are linked by sharing the same string Id (a GUID).
/// </summary>
public class DraftViewDbContext : IdentityDbContext<IdentityUser>, IUnitOfWork
{
    private readonly IUserEmailEncryptionService emailEncryptionService;
    private readonly IUserEmailLookupHmacService emailLookupHmacService;

    public DraftViewDbContext(
        DbContextOptions<DraftViewDbContext> options,
        IUserEmailEncryptionService? emailEncryptionService = null,
        IUserEmailLookupHmacService? emailLookupHmacService = null)
        : base(options)
    {
        this.emailEncryptionService = emailEncryptionService ?? new UserEmailEncryptionService();
        this.emailLookupHmacService = emailLookupHmacService ?? new UserEmailLookupHmacService();
    }

    // Domain tables
    public DbSet<User> AppUsers { get; set; } = default!;
    public DbSet<Invitation> Invitations { get; set; } = default!;
    public DbSet<ScrivenerProject> Projects { get; set; } = default!;
    public DbSet<Section> Sections { get; set; } = default!;
    public DbSet<Comment> Comments { get; set; } = default!;
    public DbSet<ReadEvent> ReadEvents { get; set; } = default!;
    public DbSet<UserPreferences> UserPreferences { get; set; } = default!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = default!;
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs { get; set; } = default!;
    public DbSet<DropboxConnection> DropboxConnections { get; set; } = default!;
    public DbSet<ReaderAccess> ReaderAccess { get; set; } = default!;
    public DbSet<SystemStateMessage> SystemStateMessages { get; set; } = default!;
    public DbSet<AuthorNotification> AuthorNotifications => Set<AuthorNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DraftViewDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        PrepareProtectedEmails();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareProtectedEmails();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareProtectedEmails();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareProtectedEmails();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    private void PrepareProtectedEmails()
    {
        var userEntries = ChangeTracker.Entries<User>()
            .Where(entry => entry.State != EntityState.Detached && entry.State != EntityState.Deleted);

        foreach (var entry in userEntries)
        {
            var user = entry.Entity;
            if (string.IsNullOrWhiteSpace(user.Email))
                continue;

            var normalizedEmail = NormalizeEmail(user.Email);
            var lookupHmac = emailLookupHmacService.Compute(normalizedEmail);

            if (entry.State == EntityState.Added ||
                string.IsNullOrWhiteSpace(user.EmailCiphertext) ||
                !string.Equals(user.EmailLookupHmac, lookupHmac, StringComparison.Ordinal))
            {
                var ciphertext = emailEncryptionService.Encrypt(normalizedEmail);
                user.LoadEmailForRuntime(normalizedEmail);
                user.SetProtectedEmail(ciphertext, lookupHmac);

                if (entry.State == EntityState.Unchanged)
                    entry.State = EntityState.Modified;
            }
        }
    }

    public static string NormalizeEmail(string email) => email.Trim();
}

