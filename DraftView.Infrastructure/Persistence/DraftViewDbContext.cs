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

    public DraftViewDbContext(DbContextOptions<DraftViewDbContext> options)
        : this(options, new UserEmailEncryptionService(), new UserEmailLookupHmacService())
    {
    }

    public DraftViewDbContext(
        DbContextOptions<DraftViewDbContext> options,
        IUserEmailEncryptionService emailEncryptionService,
        IUserEmailLookupHmacService emailLookupHmacService)
        : base(options)
    {
        this.emailEncryptionService = emailEncryptionService;
        this.emailLookupHmacService = emailLookupHmacService;
    }

    // Domain tables
    public DbSet<User> AppUsers { get; set; } = default!;
    public DbSet<Invitation> Invitations { get; set; } = default!;
    public DbSet<Project> Projects { get; set; } = default!;
    public DbSet<Section> Sections { get; set; } = default!;
    public DbSet<ReaderSnapshot> ReaderSnapshots { get; set; } = default!;
    public DbSet<PassageAnchor> PassageAnchors { get; set; } = default!;
    public DbSet<Comment> Comments { get; set; } = default!;
    public DbSet<ReadEvent> ReadEvents { get; set; } = default!;
    public DbSet<UserPreferences> UserPreferences { get; set; } = default!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = default!;
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs { get; set; } = default!;
    public DbSet<DropboxConnection> DropboxConnections { get; set; } = default!;
    public DbSet<ReaderAccess> ReaderAccess { get; set; } = default!;
    public DbSet<SystemStateMessage> SystemStateMessages { get; set; } = default!;
    public DbSet<AuthorNotification> AuthorNotifications => Set<AuthorNotification>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<ManualChapter> ManualChapters => Set<ManualChapter>();
    public DbSet<ManualChapterVersion> ManualChapterVersions => Set<ManualChapterVersion>();

    // Multi-tenancy tables (MT-Sprint-1)
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Tenancy> Tenancies => Set<Tenancy>();
    public DbSet<TenancyMembership> TenancyMemberships => Set<TenancyMembership>();

    // Multi-tenancy tables (MT-Sprint-2)
    public DbSet<TenancySubscription> TenancySubscriptions => Set<TenancySubscription>();

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
        // User entities (existing path)
        foreach (var entry in ChangeTracker.Entries<User>().Where(NeedsUserProtectedEmailReview))
        {
            if (!TryCreateProtectedEmailState(entry.Entity.Email, out var state))
                continue;
            if (!ShouldRefreshUserProtectedEmail(entry, state.LookupHmac))
                continue;

            entry.Entity.LoadEmailForRuntime(state.NormalizedEmail);
            entry.Entity.SetProtectedEmail(state.Ciphertext, state.LookupHmac);
            if (entry.State == EntityState.Unchanged)
                entry.State = EntityState.Modified;
        }

        // Account entities (MT-Sprint-1 path — same email protection scheme)
        foreach (var entry in ChangeTracker.Entries<Account>().Where(NeedsAccountProtectedEmailReview))
        {
            if (!TryCreateProtectedEmailState(entry.Entity.Email, out var state))
                continue;
            if (!ShouldRefreshAccountProtectedEmail(entry, state.LookupHmac))
                continue;

            entry.Entity.LoadEmailForRuntime(state.NormalizedEmail);
            entry.Entity.SetProtectedEmail(state.Ciphertext, state.LookupHmac);
            if (entry.State == EntityState.Unchanged)
                entry.State = EntityState.Modified;
        }
    }

    private static bool NeedsUserProtectedEmailReview(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<User> entry) =>
        entry.State != EntityState.Detached && entry.State != EntityState.Deleted;

    private static bool NeedsAccountProtectedEmailReview(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Account> entry) =>
        entry.State != EntityState.Detached && entry.State != EntityState.Deleted;

    private bool TryCreateProtectedEmailState(string email, out ProtectedEmailState protectedEmailState)
    {
        protectedEmailState = default;

        if (string.IsNullOrWhiteSpace(email))
            return false;

        var normalizedEmail = NormalizeEmail(email);
        protectedEmailState = new ProtectedEmailState(
            normalizedEmail,
            emailEncryptionService.Encrypt(normalizedEmail),
            emailLookupHmacService.Compute(normalizedEmail));

        return true;
    }

    private static bool ShouldRefreshUserProtectedEmail(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<User> entry,
        string lookupHmac) =>
        entry.State == EntityState.Added ||
        string.IsNullOrWhiteSpace(entry.Entity.EmailCiphertext) ||
        !string.Equals(entry.Entity.EmailLookupHmac, lookupHmac, StringComparison.Ordinal);

    private static bool ShouldRefreshAccountProtectedEmail(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Account> entry,
        string lookupHmac) =>
        entry.State == EntityState.Added ||
        string.IsNullOrWhiteSpace(entry.Entity.EmailCiphertext) ||
        !string.Equals(entry.Entity.EmailLookupHmac, lookupHmac, StringComparison.Ordinal);

    private readonly record struct ProtectedEmailState(
        string NormalizedEmail,
        string Ciphertext,
        string LookupHmac);

    public static string NormalizeEmail(string email) => email.Trim();
}


