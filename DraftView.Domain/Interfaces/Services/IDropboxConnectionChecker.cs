using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Checks whether Dropbox is available for sync operations.
/// Allows SyncService to skip gracefully when credentials are not configured,
/// rather than throwing and marking projects as Error.
/// </summary>
public interface IDropboxConnectionChecker
{
    /// <summary>
    /// Returns the current connection status.
    /// Does not throw -- always returns a status value.
    /// </summary>
    Task<DropboxConnectionStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns true only when Status == Connected and credentials are valid.
    /// </summary>
    Task<bool> IsConnectedAsync(CancellationToken ct = default);
}
