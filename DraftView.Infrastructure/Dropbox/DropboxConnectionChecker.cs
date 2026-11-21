using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Dropbox;

/// <summary>
/// Checks the Dropbox connection status for a specific user from the database.
/// In single-author mode, pass the author's UserId.
/// </summary>
public class DropboxConnectionChecker(IDropboxConnectionRepository connectionRepo)
    : IDropboxConnectionChecker
{
    private Guid _userId;

    public void SetUserId(Guid userId) => _userId = userId;

    public async Task<DropboxConnectionStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (_userId == Guid.Empty)
            return DropboxConnectionStatus.NotConnected;

        var connection = await connectionRepo.GetByUserIdAsync(_userId, ct);

        if (connection is null)
            return DropboxConnectionStatus.NotConnected;

        return connection.Status;
    }

    /// <summary>
    /// Returns true if a sync may proceed. An expired access token with a refresh
    /// token still counts, so the client factory can attempt the refresh.
    /// </summary>
    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        if (_userId == Guid.Empty)
            return false;

        var connection = await connectionRepo.GetByUserIdAsync(_userId, ct);
        return connection?.CanAttemptSync() ?? false;
    }
}
