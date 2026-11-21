using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class DropboxConnectionTests
{
    private static readonly Guid ValidUserId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // CreateStub
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateStub_WithValidUserId_ReturnsNotConnectedConnection()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

        Assert.NotEqual(Guid.Empty, conn.Id);
        Assert.Equal(ValidUserId, conn.UserId);
        Assert.Equal(DropboxConnectionStatus.NotConnected, conn.Status);
        Assert.Null(conn.AccessToken);
        Assert.Null(conn.RefreshToken);
        Assert.Null(conn.TokenExpiresAt);
        Assert.Null(conn.AuthorisedAt);
        Assert.Null(conn.ErrorMessage);
        Assert.True(conn.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void CreateStub_WithEmptyUserId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => DropboxConnection.CreateStub(Guid.Empty));

        Assert.Equal("I-DBOX-USER", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Authorise
    // ---------------------------------------------------------------------------

    [Fact]
    public void Authorise_WithValidTokens_SetsConnectedStatus()
    {
        var conn    = DropboxConnection.CreateStub(ValidUserId);
        var expires = DateTime.UtcNow.AddHours(4);
        var before  = DateTime.UtcNow;

        conn.Authorise("access-token", "refresh-token", expires);

        Assert.Equal(DropboxConnectionStatus.Connected, conn.Status);
        Assert.Equal("access-token", conn.AccessToken);
        Assert.Equal("refresh-token", conn.RefreshToken);
        Assert.Equal(expires, conn.TokenExpiresAt);
        Assert.NotNull(conn.AuthorisedAt);
        Assert.True(conn.AuthorisedAt >= before);
        Assert.Null(conn.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Authorise_WithInvalidAccessToken_ThrowsInvariantViolationException(string? token)
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => conn.Authorise(token, "refresh-token", DateTime.UtcNow.AddHours(4)));
#pragma warning restore CS8604

        Assert.Equal("I-DBOX-TOKEN", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Authorise_WithInvalidRefreshToken_ThrowsInvariantViolationException(string? token)
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => conn.Authorise("access-token", token, DateTime.UtcNow.AddHours(4)));
#pragma warning restore CS8604

        Assert.Equal("I-DBOX-REFRESH", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // UpdateAccessToken
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateAccessToken_WithValidToken_UpdatesTokenAndKeepsConnected()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("old-access", "refresh-token", DateTime.UtcNow.AddHours(1));
        var newExpiry = DateTime.UtcNow.AddHours(4);

        conn.UpdateAccessToken("new-access", newExpiry);

        Assert.Equal("new-access", conn.AccessToken);
        Assert.Equal(newExpiry, conn.TokenExpiresAt);
        Assert.Equal(DropboxConnectionStatus.Connected, conn.Status);
        Assert.Null(conn.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateAccessToken_WithInvalidToken_ThrowsInvariantViolationException(string? token)
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("old-access", "refresh-token", DateTime.UtcNow.AddHours(1));

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => conn.UpdateAccessToken(token, DateTime.UtcNow.AddHours(4)));
#pragma warning restore CS8604

        Assert.Equal("I-DBOX-TOKEN", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // MarkTokenExpired
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkTokenExpired_SetsTokenExpiredStatus()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));

        conn.MarkTokenExpired();

        Assert.Equal(DropboxConnectionStatus.TokenExpired, conn.Status);
        Assert.Null(conn.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // MarkRevoked
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkRevoked_SetsRevokedStatus()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));

        conn.MarkRevoked();

        Assert.Equal(DropboxConnectionStatus.Revoked, conn.Status);
        Assert.Null(conn.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // MarkError
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkError_WithMessage_SetsErrorStatus()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

        conn.MarkError("Something went wrong.");

        Assert.Equal(DropboxConnectionStatus.Error, conn.Status);
        Assert.Equal("Something went wrong.", conn.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkError_WithoutMessage_ThrowsInvariantViolationException(string? message)
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => conn.MarkError(message));
#pragma warning restore CS8604

        Assert.Equal("I-DBOX-ERR", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Disconnect
    // ---------------------------------------------------------------------------

    [Fact]
    public void Disconnect_ClearsTokensAndSetsNotConnected()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));

        conn.Disconnect();

        Assert.Equal(DropboxConnectionStatus.NotConnected, conn.Status);
        Assert.Null(conn.AccessToken);
        Assert.Null(conn.RefreshToken);
        Assert.Null(conn.TokenExpiresAt);
        Assert.Null(conn.ErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // IsTokenValid
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsTokenValid_WhenConnectedAndNotExpired_ReturnsTrue()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));

        Assert.True(conn.IsTokenValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsTokenValid_WhenExpired_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddSeconds(30));

        // Token expires in 30s but buffer is 60s so it should be considered invalid
        Assert.False(conn.IsTokenValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsTokenValid_WhenNotConnected_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

        Assert.False(conn.IsTokenValid(DateTime.UtcNow));
    }

    [Fact]
    public void IsTokenValid_WhenTokenExpiredStatus_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));
        conn.MarkTokenExpired();

        Assert.False(conn.IsTokenValid(DateTime.UtcNow));
    }

    // ---------------------------------------------------------------------------
    // CanAttemptSync
    // ---------------------------------------------------------------------------

    [Fact]
    public void CanAttemptSync_WhenConnected_ReturnsTrue()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));

        Assert.True(conn.CanAttemptSync());
    }

    [Fact]
    public void CanAttemptSync_WhenTokenExpiredWithRefreshToken_ReturnsTrue()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));
        conn.MarkTokenExpired();

        Assert.True(conn.CanAttemptSync());
    }

    [Fact]
    public void CanAttemptSync_WhenNotConnected_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);

        Assert.False(conn.CanAttemptSync());
    }

    [Fact]
    public void CanAttemptSync_WhenRevoked_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));
        conn.MarkRevoked();

        Assert.False(conn.CanAttemptSync());
    }

    [Fact]
    public void CanAttemptSync_WhenError_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));
        conn.MarkError("Something failed.");

        Assert.False(conn.CanAttemptSync());
    }

    [Fact]
    public void CanAttemptSync_WhenDisconnected_ReturnsFalse()
    {
        var conn = DropboxConnection.CreateStub(ValidUserId);
        conn.Authorise("access-token", "refresh-token", DateTime.UtcNow.AddHours(4));
        conn.Disconnect();

        Assert.False(conn.CanAttemptSync());
    }
}
