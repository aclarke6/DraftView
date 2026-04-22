using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.ValueObjects;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

/// <summary>
/// Persistence tests for passage anchors and nullable anchor references.
/// Covers: anchor snapshot round-trip, current match round-trip, repository queries,
/// and null-anchor compatibility for existing comments and read events.
/// Excludes: application authorization and UI behavior.
/// </summary>
public class PassageAnchorRepositoryTests
{
    private static DraftViewDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new DraftViewDbContext(options);
    }

    [Fact]
    public async Task AddAsync_PersistsAndReloadsAnchorWithImmutableSnapshot()
    {
        var databaseName = Guid.NewGuid().ToString();
        var anchor = CreateAnchor();

        await using (var db = CreateDb(databaseName))
        {
            var sut = new PassageAnchorRepository(db);
            await sut.AddAsync(anchor);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(databaseName))
        {
            var sut = new PassageAnchorRepository(db);

            var reloaded = await sut.GetByIdAsync(anchor.Id);

            Assert.NotNull(reloaded);
            Assert.Equal(anchor.Id, reloaded.Id);
            Assert.Equal("Selected text", reloaded.OriginalSnapshot.SelectedText);
            Assert.Equal("selected text", reloaded.OriginalSnapshot.NormalizedSelectedText);
            Assert.Equal("content-hash", reloaded.OriginalSnapshot.CanonicalContentHash);
        }
    }

    [Fact]
    public async Task AddAsync_PersistsAndReloadsCurrentMatch()
    {
        var databaseName = Guid.NewGuid().ToString();
        var anchor = CreateAnchor();
        anchor.UpdateCurrentMatch(CreateMatch());

        await using (var db = CreateDb(databaseName))
        {
            var sut = new PassageAnchorRepository(db);
            await sut.AddAsync(anchor);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(databaseName))
        {
            var sut = new PassageAnchorRepository(db);

            var reloaded = await sut.GetByIdAsync(anchor.Id);

            Assert.NotNull(reloaded);
            Assert.NotNull(reloaded.CurrentMatch);
            Assert.Equal(PassageAnchorStatus.Exact, reloaded.Status);
            Assert.Equal(95, reloaded.CurrentMatch.ConfidenceScore);
            Assert.Equal(PassageAnchorMatchMethod.Exact, reloaded.CurrentMatch.MatchMethod);
        }
    }

    [Fact]
    public async Task GetBySectionIdAsync_ReturnsOnlyAnchorsForSection()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var sectionId = Guid.NewGuid();
        var matchingAnchor = CreateAnchor(sectionId);
        var otherAnchor = CreateAnchor(Guid.NewGuid());
        var sut = new PassageAnchorRepository(db);
        await sut.AddAsync(matchingAnchor);
        await sut.AddAsync(otherAnchor);
        await db.SaveChangesAsync();

        var anchors = await sut.GetBySectionIdAsync(sectionId);

        Assert.Single(anchors);
        Assert.Equal(matchingAnchor.Id, anchors[0].Id);
    }

    [Fact]
    public async Task Comment_WithNullPassageAnchorId_PersistsAndReloads()
    {
        var databaseName = Guid.NewGuid().ToString();
        var comment = Comment.CreateRoot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Comment body",
            Visibility.Public);

        await using (var db = CreateDb(databaseName))
        {
            db.Comments.Add(comment);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(databaseName))
        {
            var reloaded = await db.Comments.SingleAsync(c => c.Id == comment.Id);

            Assert.Null(reloaded.PassageAnchorId);
        }
    }

    [Fact]
    public async Task Comment_WithPassageAnchorId_PersistsAndReloads()
    {
        var databaseName = Guid.NewGuid().ToString();
        var anchor = CreateAnchor();
        var comment = Comment.CreateRoot(
            anchor.SectionId,
            Guid.NewGuid(),
            "Comment body",
            Visibility.Public,
            passageAnchorId: anchor.Id);

        await using (var db = CreateDb(databaseName))
        {
            db.PassageAnchors.Add(anchor);
            db.Comments.Add(comment);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(databaseName))
        {
            var reloaded = await db.Comments.SingleAsync(c => c.Id == comment.Id);

            Assert.Equal(anchor.Id, reloaded.PassageAnchorId);
        }
    }

    [Fact]
    public async Task ReadEvent_WithNullResumeAnchorId_PersistsAndReloads()
    {
        var databaseName = Guid.NewGuid().ToString();
        var readEvent = ReadEvent.Create(Guid.NewGuid(), Guid.NewGuid());

        await using (var db = CreateDb(databaseName))
        {
            db.ReadEvents.Add(readEvent);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(databaseName))
        {
            var reloaded = await db.ReadEvents.SingleAsync(r => r.Id == readEvent.Id);

            Assert.Null(reloaded.ResumeAnchorId);
        }
    }

    [Fact]
    public async Task ReadEvent_WithResumeAnchorId_PersistsAndReloads()
    {
        var databaseName = Guid.NewGuid().ToString();
        var anchor = CreateAnchor();
        var readEvent = ReadEvent.Create(anchor.SectionId, Guid.NewGuid());
        readEvent.UpdateResumeAnchor(anchor.Id);

        await using (var db = CreateDb(databaseName))
        {
            db.PassageAnchors.Add(anchor);
            db.ReadEvents.Add(readEvent);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb(databaseName))
        {
            var reloaded = await db.ReadEvents.SingleAsync(r => r.Id == readEvent.Id);

            Assert.Equal(anchor.Id, reloaded.ResumeAnchorId);
        }
    }

    private static PassageAnchor CreateAnchor(Guid? sectionId = null)
    {
        return PassageAnchor.Create(
            sectionId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            PassageAnchorPurpose.Comment,
            Guid.NewGuid(),
            PassageAnchorSnapshot.Create(
                "Selected text",
                "selected text",
                "selected-hash",
                "prefix",
                "suffix",
                10,
                23,
                "content-hash"));
    }

    private static PassageAnchorMatch CreateMatch()
    {
        return PassageAnchorMatch.Create(
            Guid.NewGuid(),
            11,
            24,
            "selected text",
            95,
            PassageAnchorMatchMethod.Exact);
    }
}
