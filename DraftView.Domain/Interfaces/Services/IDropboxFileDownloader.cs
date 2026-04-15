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
}
