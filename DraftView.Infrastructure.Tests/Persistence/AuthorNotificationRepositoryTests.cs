using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;

namespace DraftView.Infrastructure.Tests.Persistence;

public class AuthorNotificationRepositoryTests : IDisposable
{
    private static readonly Guid AuthorA = Guid.NewGuid();
    private static readonly Guid AuthorB = Guid.NewGuid();

    private readonly DraftViewDbContext _db;
    private readonly AuthorNotificationRepository _sut;

    public AuthorNotificationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new DraftViewDbContext(options);
        _sut = new AuthorNotificationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static AuthorNotification Make(
        Guid authorId,
        DateTime? occurredAt = null,
        string title = "Test notification") =>
        AuthorNotification.Create(
            authorId,
            NotificationEventType.NewComment,
            title,
            null,
            null,
            occurredAt ?? DateTime.UtcNow);

    // -----------------------------------------------------------------------
    // AddAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_PersistsNotification()
    {
        var n = Make(AuthorA);
        await _sut.AddAsync(n);
        await _db.SaveChangesAsync();

        var found = await _db.AuthorNotifications.FindAsync(n.Id);
        Assert.NotNull(found);
        Assert.Equal(AuthorA, found!.AuthorId);
    }

    // -----------------------------------------------------------------------
    // GetByAuthorIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetByAuthorIdAsync_ReturnsOnlyNotificationsForAuthor()
    {
        await _sut.AddAsync(Make(AuthorA));
        await _sut.AddAsync(Make(AuthorA));
        await _sut.AddAsync(Make(AuthorB));
        await _db.SaveChangesAsync();

        var results = await _sut.GetByAuthorIdAsync(AuthorA);

        Assert.Equal(2, results.Count);
        Assert.All(results, n => Assert.Equal(AuthorA, n.AuthorId));
    }

    [Fact]
    public async Task GetByAuthorIdAsync_ReturnsOrderedByOccurredAtDescending()
    {
        var base_ = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
        await _sut.AddAsync(Make(AuthorA, base_.AddHours(-2)));
        await _sut.AddAsync(Make(AuthorA, base_));
        await _sut.AddAsync(Make(AuthorA, base_.AddHours(-1)));
        await _db.SaveChangesAsync();

        var results = await _sut.GetByAuthorIdAsync(AuthorA);

        Assert.Equal(3, results.Count);
        Assert.Equal(base_,               results[0].OccurredAt);
        Assert.Equal(base_.AddHours(-1),  results[1].OccurredAt);
        Assert.Equal(base_.AddHours(-2),  results[2].OccurredAt);
    }

    // -----------------------------------------------------------------------
    // DeleteAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesSpecificNotification()
    {
        var n1 = Make(AuthorA);
        var n2 = Make(AuthorA);
        await _sut.AddAsync(n1);
        await _sut.AddAsync(n2);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(n1.Id);
        await _db.SaveChangesAsync();

        var remaining = await _sut.GetByAuthorIdAsync(AuthorA);
        Assert.Single(remaining);
        Assert.Equal(n2.Id, remaining[0].Id);
    }

    // -----------------------------------------------------------------------
    // DeleteAllByAuthorIdAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteAllByAuthorIdAsync_RemovesAllForAuthor()
    {
        await _sut.AddAsync(Make(AuthorA));
        await _sut.AddAsync(Make(AuthorA));
        await _db.SaveChangesAsync();

        await _sut.DeleteAllByAuthorIdAsync(AuthorA);
        await _db.SaveChangesAsync();

        var remaining = await _sut.GetByAuthorIdAsync(AuthorA);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task DeleteAllByAuthorIdAsync_DoesNotAffectOtherAuthors()
    {
        await _sut.AddAsync(Make(AuthorA));
        await _sut.AddAsync(Make(AuthorB));
        await _db.SaveChangesAsync();

        await _sut.DeleteAllByAuthorIdAsync(AuthorA);
        await _db.SaveChangesAsync();

        var bRemaining = await _sut.GetByAuthorIdAsync(AuthorB);
        Assert.Single(bRemaining);
    }

    // -----------------------------------------------------------------------
    // PruneOlderThanAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PruneOlderThanAsync_RemovesOldNotifications()
    {
        var cutoff = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        await _sut.AddAsync(Make(AuthorA, cutoff.AddDays(-1)));
        await _sut.AddAsync(Make(AuthorA, cutoff.AddDays(-10)));
        await _db.SaveChangesAsync();

        await _sut.PruneOlderThanAsync(AuthorA, cutoff);
        await _db.SaveChangesAsync();

        var remaining = await _sut.GetByAuthorIdAsync(AuthorA);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task PruneOlderThanAsync_PreservesRecentNotifications()
    {
        var cutoff = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        await _sut.AddAsync(Make(AuthorA, cutoff.AddDays(-1)));  // old
        await _sut.AddAsync(Make(AuthorA, cutoff.AddDays(1)));   // recent
        await _db.SaveChangesAsync();

        await _sut.PruneOlderThanAsync(AuthorA, cutoff);
        await _db.SaveChangesAsync();

        var remaining = await _sut.GetByAuthorIdAsync(AuthorA);
        Assert.Single(remaining);
        Assert.Equal(cutoff.AddDays(1), remaining[0].OccurredAt);
    }
}
