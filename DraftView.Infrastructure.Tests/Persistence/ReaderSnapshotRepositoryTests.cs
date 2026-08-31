using DraftView.Domain.Entities;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class ReaderSnapshotRepositoryTests
{
    private static DraftViewDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DraftViewDbContext(options);
    }

    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // GetAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoSnapshotExists()
    {
        using var db = CreateDb();
        var sut = new ReaderSnapshotRepository(db);

        var result = await sut.GetAsync(SectionId, UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ReturnsSnapshot_WhenExists()
    {
        using var db = CreateDb();
        var snapshot = ReaderSnapshot.Create(SectionId, UserId, "<p>Hello</p>");
        db.ReaderSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var sut    = new ReaderSnapshotRepository(db);
        var result = await sut.GetAsync(SectionId, UserId);

        Assert.NotNull(result);
        Assert.Equal(SectionId,    result!.SectionId);
        Assert.Equal(UserId,       result.UserId);
        Assert.Equal("<p>Hello</p>", result.HtmlContent);
    }

    [Fact]
    public async Task GetAsync_DoesNotReturnSnapshotForDifferentUser()
    {
        using var db       = CreateDb();
        var otherUserId    = Guid.NewGuid();
        var snapshot = ReaderSnapshot.Create(SectionId, otherUserId, "<p>Hello</p>");
        db.ReaderSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var sut    = new ReaderSnapshotRepository(db);
        var result = await sut.GetAsync(SectionId, UserId);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // UpsertAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpsertAsync_InsertsNewSnapshot_WhenNoneExists()
    {
        using var db = CreateDb();
        var sut      = new ReaderSnapshotRepository(db);
        var snapshot = ReaderSnapshot.Create(SectionId, UserId, "<p>First</p>");

        await sut.UpsertAsync(snapshot);
        await db.SaveChangesAsync();

        var result = await db.ReaderSnapshots
            .FirstOrDefaultAsync(s => s.SectionId == SectionId && s.UserId == UserId);
        Assert.NotNull(result);
        Assert.Equal("<p>First</p>", result!.HtmlContent);
    }

    [Fact]
    public async Task UpsertAsync_ReplacesExistingSnapshot_WhenAlreadyExists()
    {
        using var db  = CreateDb();
        var original  = ReaderSnapshot.Create(SectionId, UserId, "<p>Original</p>");
        db.ReaderSnapshots.Add(original);
        await db.SaveChangesAsync();

        var sut     = new ReaderSnapshotRepository(db);
        var updated = ReaderSnapshot.Create(SectionId, UserId, "<p>Updated</p>");
        await sut.UpsertAsync(updated);
        await db.SaveChangesAsync();

        var snapshots = await db.ReaderSnapshots
            .Where(s => s.SectionId == SectionId && s.UserId == UserId)
            .ToListAsync();
        Assert.Single(snapshots);
        Assert.Equal("<p>Updated</p>", snapshots[0].HtmlContent);
    }

    // ---------------------------------------------------------------------------
    // GetBySectionIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetBySectionIdAsync_ReturnsAllSnapshotsForSection()
    {
        using var db   = CreateDb();
        var userId2    = Guid.NewGuid();
        var snapA = ReaderSnapshot.Create(SectionId, UserId,  "<p>A</p>");
        var snapB = ReaderSnapshot.Create(SectionId, userId2, "<p>B</p>");
        var snapC = ReaderSnapshot.Create(Guid.NewGuid(), UserId, "<p>C</p>"); // different section
        db.ReaderSnapshots.AddRange(snapA, snapB, snapC);
        await db.SaveChangesAsync();

        var sut    = new ReaderSnapshotRepository(db);
        var result = await sut.GetBySectionIdAsync(SectionId);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal(SectionId, s.SectionId));
    }

    [Fact]
    public async Task GetBySectionIdAsync_ReturnsEmptyList_WhenNoSnapshots()
    {
        using var db = CreateDb();
        var sut      = new ReaderSnapshotRepository(db);

        var result = await sut.GetBySectionIdAsync(SectionId);

        Assert.Empty(result);
    }
}
