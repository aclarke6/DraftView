using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class SystemStateMessageServiceTests
{
    private readonly Mock<ISystemStateMessageRepository> MessageRepo = new();
    private readonly Mock<IUnitOfWork>                   UnitOfWork  = new();
    private readonly Mock<IAuthorizationFacade>          AuthFacade  = new();

    private SystemStateMessageService CreateSut() => new(
        MessageRepo.Object,
        UnitOfWork.Object,
        AuthFacade.Object);

    // ---------------------------------------------------------------------------
    // CreateMessageAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateMessageAsync_WhenSystemSupport_CreatesAndReturnsMessage()
    {
        AuthFacade.Setup(f => f.IsSystemSupport()).Returns(true);
        MessageRepo.Setup(r => r.GetActiveAsync(default)).ReturnsAsync((SystemStateMessage?)null);

        var sut    = CreateSut();
        var result = await sut.CreateMessageAsync("Scheduled maintenance tonight.");

        Assert.NotNull(result);
        Assert.Equal("Scheduled maintenance tonight.", result.Message);
        Assert.True(result.IsActive);
        MessageRepo.Verify(r => r.AddAsync(result, default), Times.Once);
        UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateMessageAsync_WhenNotSystemSupport_ThrowsUnauthorisedOperationException()
    {
        AuthFacade.Setup(f => f.IsSystemSupport()).Returns(false);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.CreateMessageAsync("Test message."));
    }

    [Fact]
    public async Task CreateMessageAsync_DeactivatesExistingActiveMessage()
    {
        var existing = SystemStateMessage.Create("Old message.", Guid.NewGuid());
        AuthFacade.Setup(f => f.IsSystemSupport()).Returns(true);
        MessageRepo.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(existing);

        var sut = CreateSut();
        await sut.CreateMessageAsync("New message.");

        Assert.False(existing.IsActive);
        Assert.NotNull(existing.DeactivatedAt);
    }

    // ---------------------------------------------------------------------------
    // DeactivateMessageAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateMessageAsync_WhenSystemSupport_DeactivatesMessage()
    {
        var msg = SystemStateMessage.Create("Active message.", Guid.NewGuid());
        AuthFacade.Setup(f => f.IsSystemSupport()).Returns(true);
        MessageRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<SystemStateMessage> { msg });

        var sut = CreateSut();
        await sut.DeactivateMessageAsync(msg.Id);

        Assert.False(msg.IsActive);
        UnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DeactivateMessageAsync_WhenNotSystemSupport_ThrowsUnauthorisedOperationException()
    {
        AuthFacade.Setup(f => f.IsSystemSupport()).Returns(false);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.DeactivateMessageAsync(Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------------
    // GetActiveMessageAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetActiveMessageAsync_ReturnsActiveMessage()
    {
        var msg = SystemStateMessage.Create("Active message.", Guid.NewGuid());
        MessageRepo.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(msg);

        var sut    = CreateSut();
        var result = await sut.GetActiveMessageAsync();

        Assert.Same(msg, result);
    }

    // ---------------------------------------------------------------------------
    // GetAllMessagesAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllMessagesAsync_WhenNotSystemSupport_ThrowsUnauthorisedOperationException()
    {
        AuthFacade.Setup(f => f.IsSystemSupport()).Returns(false);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.GetAllMessagesAsync());
    }
}
