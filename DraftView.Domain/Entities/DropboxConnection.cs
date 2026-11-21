using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class DropboxConnection
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? TokenExpiresAt { get; private set; }
    public DateTime? AuthorisedAt { get; private set; }
    public DropboxConnectionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private DropboxConnection() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a stub DropboxConnection for a user who has not yet connected.
    /// </summary>
    public static DropboxConnection CreateStub(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new InvariantViolationException("I-DBOX-USER",
                "DropboxConnection must be associated with a valid user.");

        return new DropboxConnection
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Status    = DropboxConnectionStatus.NotConnected,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Records a successful OAuth authorisation, storing the tokens.
    /// </summary>
    public void Authorise(string accessToken, string refreshToken, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvariantViolationException("I-DBOX-TOKEN",
                "Access token must not be null or whitespace.");

        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvariantViolationException("I-DBOX-REFRESH",
                "Refresh token must not be null or whitespace.");

        AccessToken    = accessToken;
        RefreshToken   = refreshToken;
        TokenExpiresAt = expiresAt;
        AuthorisedAt   = DateTime.UtcNow;
        Status         = DropboxConnectionStatus.Connected;
        ErrorMessage   = null;
    }

    /// <summary>
    /// Updates the access token after a successful refresh.
    /// </summary>
    public void UpdateAccessToken(string accessToken, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvariantViolationException("I-DBOX-TOKEN",
                "Access token must not be null or whitespace.");

        AccessToken    = accessToken;
        TokenExpiresAt = expiresAt;
        Status         = DropboxConnectionStatus.Connected;
        ErrorMessage   = null;
    }

    /// <summary>
    /// Marks the connection as having an expired token requiring refresh.
    /// </summary>
    public void MarkTokenExpired()
    {
        Status       = DropboxConnectionStatus.TokenExpired;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the connection as revoked (user removed access in Dropbox).
    /// </summary>
    public void MarkRevoked()
    {
        Status       = DropboxConnectionStatus.Revoked;
        ErrorMessage = null;
    }

    /// <summary>
    /// Marks the connection as in an error state.
    /// </summary>
    public void MarkError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new InvariantViolationException("I-DBOX-ERR",
                "An error message is required when marking a connection as errored.");

        Status       = DropboxConnectionStatus.Error;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Disconnects the user, clearing tokens.
    /// </summary>
    public void Disconnect()
    {
        AccessToken    = null;
        RefreshToken   = null;
        TokenExpiresAt = null;
        Status         = DropboxConnectionStatus.NotConnected;
        ErrorMessage   = null;
    }

    /// <summary>
    /// Returns true if a sync may be attempted: the connection is Connected, or the
    /// access token has expired but a refresh token is available to renew it.
    /// </summary>
    public bool CanAttemptSync() =>
        Status == DropboxConnectionStatus.Connected ||
        (Status == DropboxConnectionStatus.TokenExpired &&
         !string.IsNullOrWhiteSpace(RefreshToken));

    /// <summary>
    /// Returns true if the access token is present and not yet expired.
    /// Includes a 60-second buffer to allow for clock skew.
    /// </summary>
    public bool IsTokenValid(DateTime utcNow) =>
        Status == DropboxConnectionStatus.Connected &&
        !string.IsNullOrWhiteSpace(AccessToken) &&
        TokenExpiresAt.HasValue &&
        TokenExpiresAt.Value > utcNow.AddSeconds(60);
}
