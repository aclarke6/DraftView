namespace DraftView.Domain.Interfaces.Services;

public sealed class DropboxFileInfo
{
    public string Path { get; init; } = default!;
    public string Name { get; init; } = default!;
    public long Size { get; init; }
    public DateTime ServerModified { get; init; }
}

public interface IDropboxClient
{
    /// <summary>
    /// Lists all .scriv folders in the configured Dropbox path.
    /// </summary>
    Task<IReadOnlyList<DropboxFileInfo>> ListScrivFoldersAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads a single file from Dropbox to a local destination path.
    /// </summary>
    Task DownloadFileAsync(string dropboxPath, string localDestPath, CancellationToken ct = default);

    /// <summary>
    /// Downloads all files under a Dropbox folder path to a local destination folder,
    /// preserving relative paths. Returns the list of downloaded files.
    /// </summary>
    Task<IReadOnlyList<string>> DownloadFolderAsync(
        string dropboxFolderPath, string localDestFolder, CancellationToken ct = default);

    /// <summary>
    /// Lists files under a Dropbox folder path recursively.
    /// </summary>
    Task<IReadOnlyList<DropboxFileInfo>> ListFilesAsync(
        string dropboxFolderPath, CancellationToken ct = default);

    /// <summary>
    /// Returns the latest cursor for a given Dropbox path, used for change detection.
    /// </summary>
    Task<string> GetLatestCursorAsync(string dropboxFolderPath, CancellationToken ct = default);

    /// <summary>
    /// Checks whether files have changed since the given cursor.
    /// Returns the new cursor and whether changes were detected.
    /// </summary>
    Task<(bool HasChanges, string NewCursor)> CheckForChangesAsync(
        string cursor, CancellationToken ct = default);

    /// <summary>
    /// Performs a full folder listing and returns file entries with the resulting cursor.
    /// </summary>
    Task<(IReadOnlyList<DropboxChangedEntry> Entries, string Cursor)> ListAllEntriesWithCursorAsync(
        string dropboxFolderPath,
        CancellationToken ct = default);

    /// <summary>
    /// Lists changed entries since a prior cursor and returns the new cursor.
    /// </summary>
    Task<(IReadOnlyList<DropboxChangedEntry> Entries, string Cursor)> ListChangedEntriesAsync(
        string cursor,
        CancellationToken ct = default);
}
