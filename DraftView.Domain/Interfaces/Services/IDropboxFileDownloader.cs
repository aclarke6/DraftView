using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IDropboxFileDownloader
{
    /// <summary>
    /// Downloads the Scrivener project folder from Dropbox into the local
    /// per-author cache path. Returns the local path to the downloaded folder.
    /// </summary>
    Task<string> DownloadProjectAsync(
        Project project,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns changed Dropbox entries and the new cursor for an existing cursor position.
    /// </summary>
    Task<(IReadOnlyList<DropboxChangedEntry> Entries, string NewCursor)> ListChangedEntriesAsync(
        Guid userId,
        string cursor,
        CancellationToken ct = default);

    /// <summary>
    /// Performs a full Dropbox listing and returns all entries with an initial cursor.
    /// </summary>
    Task<(IReadOnlyList<DropboxChangedEntry> Entries, string InitialCursor)> ListAllEntriesWithCursorAsync(
        Guid userId,
        string dropboxPath,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads only Added/Modified entries for a project into local cache and returns the resolved local project path.
    /// </summary>
    Task<string> DownloadChangedEntriesAsync(
        Project project,
        Guid userId,
        IReadOnlyList<DropboxChangedEntry> entries,
        CancellationToken ct = default);
}
