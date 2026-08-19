using DraftView.Domain.Entities;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests for ManualChapterRepository against an in-memory EF database.
/// Covers: persistence, author scoping, ordering, update, delete, and chapter count.
/// Excludes: version history (ManualChapterVersionRepositoryTests), application service logic.
/// </summary>
public class ManualChapterRepositoryTests : IDisposable
{
    private static readonly Guid AuthorA  = Guid.NewGuid();
    private static readonly Guid AuthorB  = Guid.NewGuid();
    private static readonly Guid ProjectA = Guid.NewGuid();
    private static readonly Guid ProjectB = Guid.NewGuid();

    private readonly DraftViewDbContext        _db;
    private readonly ManualChapterRepository   _sut;

    public ManualChapterRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new DraftViewDbContext(options);
        _sut = new ManualChapterRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static ManualChapter MakeChapter(
        Guid authorId,
        Guid projectId,
        int sortOrder = 0,
        string title = "Chapter") =>
        ManualChapter.Create(
            authorId, projectId, title, sortOrder,
            "content", "file.txt",
            new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc));

    // ── AddAsync / GetByProjectAsync ─────────────────────────────────────────

    [Fact]
    public async Task AddAsync_PersistsChapter()
    {
        var chapter = MakeChapter(AuthorA, ProjectA);
        await _sut.AddAsync(chapter);
        await _db.SaveChangesAsync();

        var found = await _db.ManualChapters.FindAsync(chapter.Id);
        Assert.NotNull(found);
        Assert.Equal(AuthorA,  found!.AuthorId);
        Assert.Equal(ProjectA, found!.ProjectId);
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsChaptersForAuthorAndProject()
    {
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA, 0, "Ch 1"));
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA, 1, "Ch 2"));
        await _sut.AddAsync(MakeChapter(AuthorB, ProjectA, 0, "Other Author"));
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectB, 0, "Other Project"));
        await _db.SaveChangesAsync();

        var results = await _sut.GetByProjectAsync(ProjectA, AuthorA);

        Assert.Equal(2, results.Count);
        Assert.All(results, c =>
        {
            Assert.Equal(AuthorA,  c.AuthorId);
            Assert.Equal(ProjectA, c.ProjectId);
        });
    }

    [Fact]
    public async Task GetByProjectAsync_ReturnsChaptersOrderedBySortOrder()
    {
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA, 2, "Ch 3"));
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA, 0, "Ch 1"));
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA, 1, "Ch 2"));
        await _db.SaveChangesAsync();

        var results = await _sut.GetByProjectAsync(ProjectA, AuthorA);

        Assert.Equal("Ch 1", results[0].Title);
        Assert.Equal("Ch 2", results[1].Title);
        Assert.Equal("Ch 3", results[2].Title);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsChapterWhenFound()
    {
        var chapter = MakeChapter(AuthorA, ProjectA);
        await _sut.AddAsync(chapter);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(chapter.Id, AuthorA);

        Assert.NotNull(found);
        Assert.Equal(chapter.Id, found!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenAuthorMismatch()
    {
        var chapter = MakeChapter(AuthorA, ProjectA);
        await _sut.AddAsync(chapter);
        await _db.SaveChangesAsync();

        var found = await _sut.GetByIdAsync(chapter.Id, AuthorB);

        Assert.Null(found);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_PersistsContentChange()
    {
        var chapter = MakeChapter(AuthorA, ProjectA);
        await _sut.AddAsync(chapter);
        await _db.SaveChangesAsync();

        chapter.UpdateContent("updated content");
        await _sut.UpdateAsync(chapter);
        await _db.SaveChangesAsync();

        var found = await _db.ManualChapters.FindAsync(chapter.Id);
        Assert.Equal("updated content", found!.RawContent);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesChapter()
    {
        var chapter = MakeChapter(AuthorA, ProjectA);
        await _sut.AddAsync(chapter);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(chapter.Id, AuthorA);
        await _db.SaveChangesAsync();

        var found = await _db.ManualChapters.FindAsync(chapter.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteAsync_DoesNothing_WhenAuthorMismatch()
    {
        var chapter = MakeChapter(AuthorA, ProjectA);
        await _sut.AddAsync(chapter);
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(chapter.Id, AuthorB);
        await _db.SaveChangesAsync();

        var found = await _db.ManualChapters.FindAsync(chapter.Id);
        Assert.NotNull(found);
    }

    // ── CountByProjectAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CountByProjectAsync_ReturnsCorrectCount()
    {
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA));
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectA));
        await _sut.AddAsync(MakeChapter(AuthorA, ProjectB));
        await _db.SaveChangesAsync();

        var count = await _sut.CountByProjectAsync(ProjectA, AuthorA);

        Assert.Equal(2, count);
    }
}
