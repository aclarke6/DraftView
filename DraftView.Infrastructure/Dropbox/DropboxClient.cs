using Dropbox.Api;
using Dropbox.Api.Files;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Dropbox;

public class DropboxClient : IDropboxClient, IDisposable
{
    private readonly global::Dropbox.Api.DropboxClient _client;
    private readonly DropboxClientSettings _settings;

    public DropboxClient(DropboxClientSettings settings)
    {
        _settings = settings;
        _client   = new global::Dropbox.Api.DropboxClient(settings.AccessToken);
    }

    // ---------------------------------------------------------------------------
    // ListScrivFolders
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<DropboxFileInfo>> ListScrivFoldersAsync(
        CancellationToken ct = default)
    {
        var result = await _client.Files.ListFolderAsync(
            new ListFolderArg(_settings.DropboxScrivenerPath));

        var folders = new List<DropboxFileInfo>();

        do
        {
            foreach (var entry in result.Entries)
            {
                if (entry.IsFolder && entry.Name.EndsWith(".scriv", StringComparison.OrdinalIgnoreCase))
                {
                    folders.Add(new DropboxFileInfo
                    {
                        Path           = entry.PathLower ?? entry.PathDisplay,
                        Name           = entry.Name,
                        Size           = 0,
                        ServerModified = DateTime.UtcNow
                    });
                }
            }

            if (!result.HasMore) break;
            result = await _client.Files.ListFolderContinueAsync(result.Cursor);
        }
        while (true);

        return folders;
    }

    // ---------------------------------------------------------------------------
    // ListFiles
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<DropboxFileInfo>> ListFilesAsync(
        string dropboxFolderPath, CancellationToken ct = default)
    {
        var result = await _client.Files.ListFolderAsync(
            new ListFolderArg(dropboxFolderPath, recursive: true));

        var files = new List<DropboxFileInfo>();

        do
        {
            foreach (var entry in result.Entries)
            {
                if (entry.IsFile)
                {
                    var file = entry.AsFile;
                    files.Add(new DropboxFileInfo
                    {
                        Path           = file.PathLower ?? file.PathDisplay,
                        Name           = file.Name,
                        Size           = (long)file.Size,
                        ServerModified = file.ServerModified
                    });
                }
            }

            if (!result.HasMore) break;
            result = await _client.Files.ListFolderContinueAsync(result.Cursor);
        }
        while (true);

        return files;
    }

    // ---------------------------------------------------------------------------
    // DownloadFile
    // ---------------------------------------------------------------------------

    public async Task DownloadFileAsync(
        string dropboxPath, string localDestPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(localDestPath)!);

        using var response = await _client.Files.DownloadAsync(dropboxPath);
        var bytes = await response.GetContentAsByteArrayAsync();
        await File.WriteAllBytesAsync(localDestPath, bytes, ct);
    }

    // ---------------------------------------------------------------------------
    // DownloadFolder
    // ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<string>> DownloadFolderAsync(
        string dropboxFolderPath, string localDestFolder, CancellationToken ct = default)
    {
        var files       = await ListFilesAsync(dropboxFolderPath, ct);
        var downloaded  = new List<string>();

        foreach (var file in files)
        {
            // Preserve relative path structure under the local dest folder
            var relativePath = file.Path[dropboxFolderPath.Length..].TrimStart('/');
            var localPath    = Path.Combine(localDestFolder,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            await DownloadFileAsync(file.Path, localPath, ct);
            downloaded.Add(localPath);
        }

        return downloaded;
    }

    // ---------------------------------------------------------------------------
    // Change detection via longpoll cursor
    // ---------------------------------------------------------------------------

    public async Task<string> GetLatestCursorAsync(
        string dropboxFolderPath, CancellationToken ct = default)
    {
        var result = await _client.Files.ListFolderGetLatestCursorAsync(
            new ListFolderArg(dropboxFolderPath, recursive: true));
        return result.Cursor;
    }

    public async Task<(bool HasChanges, string NewCursor)> CheckForChangesAsync(
        string cursor, CancellationToken ct = default)
    {
        var result = await _client.Files.ListFolderContinueAsync(cursor);
        var hasChanges = result.Entries.Count > 0 || result.HasMore;
        return (hasChanges, result.Cursor);
    }

    public async Task<(IReadOnlyList<DropboxChangedEntry> Entries, string Cursor)> ListAllEntriesWithCursorAsync(
        string dropboxFolderPath,
        CancellationToken ct = default)
    {
        var result = await _client.Files.ListFolderAsync(
            new ListFolderArg(dropboxFolderPath, recursive: true));

        var entries = MapEntries(result.Entries, DropboxEntryType.Added);

        while (result.HasMore)
        {
            result = await _client.Files.ListFolderContinueAsync(result.Cursor);
            entries.AddRange(MapEntries(result.Entries, DropboxEntryType.Added));
        }

        return (entries, result.Cursor);
    }

    public async Task<(IReadOnlyList<DropboxChangedEntry> Entries, string Cursor)> ListChangedEntriesAsync(
        string cursor,
        CancellationToken ct = default)
    {
        var result = await _client.Files.ListFolderContinueAsync(cursor);
        var entries = MapEntries(result.Entries, DropboxEntryType.Modified);

        while (result.HasMore)
        {
            result = await _client.Files.ListFolderContinueAsync(result.Cursor);
            entries.AddRange(MapEntries(result.Entries, DropboxEntryType.Modified));
        }

        return (entries, result.Cursor);
    }

    private static List<DropboxChangedEntry> MapEntries(
        IEnumerable<Metadata> entries,
        DropboxEntryType fileEntryType)
    {
        var mapped = new List<DropboxChangedEntry>();

        foreach (var entry in entries)
        {
            if (entry.IsDeleted)
            {
                var deleted = entry.AsDeleted;
                var deletedPath = deleted.PathLower ?? deleted.PathDisplay;
                mapped.Add(new DropboxChangedEntry(deletedPath, DropboxEntryType.Deleted, null));
                continue;
            }

            if (!entry.IsFile)
                continue;

            var file = entry.AsFile;
            var filePath = file.PathLower ?? file.PathDisplay;
            mapped.Add(new DropboxChangedEntry(filePath, fileEntryType, file.ContentHash));
        }

        return mapped;
    }

    public void Dispose() => _client.Dispose();
}
