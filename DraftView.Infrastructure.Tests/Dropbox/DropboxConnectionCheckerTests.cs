using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Dropbox;
using Moq;

namespace DraftView.Infrastructure.Tests.Dropbox;

public class DropboxConnectionCheckerTests
{
    private readonly Mock<IDropboxConnectionRepository> _connectionRepo = new();
    private readonly Guid _userId = Guid.NewGuid();

    private DropboxConnectionChecker CreateSut(DropboxConnection? connection)
    {
        _connectionRepo.Setup(r => r.GetByUserIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var sut = new DropboxConnectionChecker(_connectionRepo.Object);
        sut.SetUserId(_userId);
        return sut;
    }

    private DropboxConnection CreateAuthorisedConnection()
    {
        var conn = DropboxConnection.CreateStub(_userId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));
        return conn;
    }

    [Fact]
    public async Task IsConnectedAsync_WhenConnected_ReturnsTrue()
    {
        var sut = CreateSut(CreateAuthorisedConnection());

        Assert.True(await sut.IsConnectedAsync());
    }

    [Fact]
    public async Task IsConnectedAsync_WhenTokenExpiredWithRefreshToken_ReturnsTrue()
    {
        var conn = CreateAuthorisedConnection();
        conn.MarkTokenExpired();
        var sut = CreateSut(conn);

        Assert.True(await sut.IsConnectedAsync());
    }

    [Fact]
    public async Task IsConnectedAsync_WhenRevoked_ReturnsFalse()
    {
        var conn = CreateAuthorisedConnection();
        conn.MarkRevoked();
        var sut = CreateSut(conn);

        Assert.False(await sut.IsConnectedAsync());
    }

    [Fact]
    public async Task IsConnectedAsync_WhenNoConnection_ReturnsFalse()
    {
        var sut = CreateSut(null);

        Assert.False(await sut.IsConnectedAsync());
    }
}
