using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Infrastructure.Persistence;

/// <summary>
/// Single DbContext hosting both ASP.NET Core Identity tables (for authentication)
/// and DraftView domain tables (for business logic).
/// IdentityUser handles login only. Domain User handles roles, comments, invitations.
/// The two are linked by sharing the same string Id (a GUID).
/// </summary>
public class DraftViewDbContext(DbContextOptions<DraftViewDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IUnitOfWork
{
    // Domain tables
    public DbSet<User> AppUsers { get; set; } = default!;
    public DbSet<Invitation> Invitations { get; set; } = default!;
    public DbSet<ScrivenerProject> Projects { get; set; } = default!;
    public DbSet<Section> Sections { get; set; } = default!;
    public DbSet<Comment> Comments { get; set; } = default!;
    public DbSet<ReadEvent> ReadEvents { get; set; } = default!;
    public DbSet<UserNotificationPreferences> NotificationPreferences { get; set; } = default!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = default!;
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DraftViewDbContext).Assembly);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct) =>
        base.SaveChangesAsync(ct);
}

