using DraftView.Domain.Entities;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class SectionVersionRepositoryTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();

    private static DraftViewDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftViewDbContext(options);
    }

    [Fact]
    public async Task GetVersionCountAsync_ReturnsCorrectCount()
    {
        using var db = CreateDb();
        var section = MakeSection();
        var otherSection = MakeSection();

        db.SectionVersions.Add(SectionVersion.Create(section, AuthorId, 1));
        db.SectionVersions.Add(SectionVersion.Create(section, AuthorId, 2));
        db.SectionVersions.Add(SectionVersion.Create(section, AuthorId, 3));
        db.SectionVersions.Add(SectionVersion.Create(otherSection, AuthorId, 1));
        await db.SaveChangesAsync();

        var sut = new SectionVersionRepository(db);

        var count = await sut.GetVersionCountAsync(section.Id);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetVersionCountAsync_WhenNoVersions_ReturnsZero()
    {
        using var db = CreateDb();
        var sut = new SectionVersionRepository(db);

        var count = await sut.GetVersionCountAsync(Guid.NewGuid());

        Assert.Equal(0, count);
    }

    private static Section MakeSection() =>
        Section.CreateDocument(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "Section",
            null,
            1,
            "<p>content</p>",
            "hash",
            null);
}
