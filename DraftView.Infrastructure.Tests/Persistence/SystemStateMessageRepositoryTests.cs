using DraftView.Domain.Entities;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Tests.Persistence;

public class SystemStateMessageRepositoryTests
{
    private static DraftViewDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DraftViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DraftViewDbContext(options);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectMessage()
    {
        using var db  = CreateDb();
        var userId    = Guid.NewGuid();
        var msgA      = SystemStateMessage.Create("Message A", userId);
        var msgB      = SystemStateMessage.Create("Message B", userId);
        db.SystemStateMessages.AddRange(msgA, msgB);
        await db.SaveChangesAsync();

        var sut    = new SystemStateMessageRepository(db);
        var result = await sut.GetByIdAsync(msgA.Id);

        Assert.NotNull(result);
        Assert.Equal(msgA.Id, result!.Id);
        Assert.Equal("Message A", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        using var db = CreateDb();
        var sut      = new SystemStateMessageRepository(db);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
