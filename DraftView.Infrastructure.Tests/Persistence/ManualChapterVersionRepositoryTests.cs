using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for ManualChapterVersionRepository against an in-memory EF database.
/// Covers: persistence, ordered listing, ClearForChapterAsync hard-delete,
/// and GetNextVersionNumberAsync sequencing.
/// Excludes: ManualChapter persistence (ManualChapterRepositoryTests), application service logic.
/// </summary>
public class ManualChapterVersionRepositoryTests : IDisposable
{
    private static readonly Guid AuthorA = Guid.NewGuid();

    private readonly DraftViewDbContext               _db;
    private readonly ManualChapterVersionRepository   _sut;

    public ManualChapterVersionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new DraftViewDbContext(options);
        _sut = new ManualChapterVersionRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static ManualChapterVersion MakeVersion(
        Guid chapterId,
        int versionNumber = 1,
        string content = "content",
        SnapshotReason reason = SnapshotReason.FileUpload) =>
        ManualChapterVersion.Create(
            chapterId, AuthorA, versionNumber, content, reason,
            new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc).AddMinutes(versionNumber));

    // ── AddAsync / GetByChapterAsync ─────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsVersion()
    {
        var chapterId = Guid.NewGuid();
        var version   = MakeVersion(chapterId);
        await _sut.AddAsync(version);
        await _db.SaveChangesAsync();

        var found = await _db.ManualChapterVersions.FindAsync(version.Id);
        Assert.NotNull(found);
        Assert.Equal(chapterId, found!.ManualChapterId);
    }

    [Fact]
    public async Task GetByChapterAsync_ReturnsVersionsForChapter()
    {
        var chapterA = Guid.NewGuid();
        var chapterB = Guid.NewGuid();

        await _sut.AddAsync(MakeVersion(chapterA, 1));
        await _sut.AddAsync(MakeVersion(chapterA, 2));
        await _sut.AddAsync(MakeVersion(chapterB, 1));
        await _db.SaveChangesAsync();

        var results = await _sut.GetByChapterAsync(chapterA);

        Assert.Equal(2, results.Count);
        Assert.All(results, v => Assert.Equal(chapterA, v.ManualChapterId));
    }

    [Fact]
    public async Task GetByChapterAsync_ReturnsVersionsDescendingByVersionNumber()
    {
        var chapterId = Guid.NewGuid();
        await _sut.AddAsync(MakeVersion(chapterId, 1));
        await _sut.AddAsync(MakeVersion(chapterId, 3));
        await _sut.AddAsync(MakeVersion(chapterId, 2));
        await _db.SaveChangesAsync();

        var results = await _sut.GetByChapterAsync(chapterId);

        Assert.Equal(3, results[0].VersionNumber);
        Assert.Equal(2, results[1].VersionNumber);
        Assert.Equal(1, results[2].VersionNumber);
    }

    // ── ClearForChapterAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ClearForChapterAsync_HardDeletesAllVersionsForChapter()
    {
        var chapterId = Guid.NewGuid();
        await _sut.AddAsync(MakeVersion(chapterId, 1));
        await _sut.AddAsync(MakeVersion(chapterId, 2));
        await _db.SaveChangesAsync();

        await _sut.ClearForChapterAsync(chapterId);
        await _db.SaveChangesAsync();

        var remaining = await _sut.GetByChapterAsync(chapterId);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task ClearForChapterAsync_DoesNotAffectOtherChapters()
    {
        var chapterA = Guid.NewGuid();
        var chapterB = Guid.NewGuid();

        await _sut.AddAsync(MakeVersion(chapterA, 1));
        await _sut.AddAsync(MakeVersion(chapterB, 1));
        await _db.SaveChangesAsync();

        await _sut.ClearForChapterAsync(chapterA);
        await _db.SaveChangesAsync();

        var bRemaining = await _sut.GetByChapterAsync(chapterB);
        Assert.Single(bRemaining);
    }

    // ── GetNextVersionNumberAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetNextVersionNumberAsync_ReturnsOne_WhenNoVersionsExist()
    {
        var chapterId = Guid.NewGuid();

        var next = await _sut.GetNextVersionNumberAsync(chapterId);

        Assert.Equal(1, next);
    }

    [Fact]
    public async Task GetNextVersionNumberAsync_ReturnsMaxPlusOne()
    {
        var chapterId = Guid.NewGuid();
        await _sut.AddAsync(MakeVersion(chapterId, 1));
        await _sut.AddAsync(MakeVersion(chapterId, 2));
        await _db.SaveChangesAsync();

        var next = await _sut.GetNextVersionNumberAsync(chapterId);

        Assert.Equal(3, next);
    }
}
