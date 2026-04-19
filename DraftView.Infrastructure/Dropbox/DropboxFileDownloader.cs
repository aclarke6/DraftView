using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace DraftView.Infrastructure.Dropbox;

public class DropboxFileDownloader(
    IDropboxClientFactory clientFactory,
    ILocalPathResolver pathResolver,
    ISyncProgressTracker progressTracker,
    ILogger<DropboxFileDownloader> logger) : IDropboxFileDownloader
{
    public async Task<string> DownloadProjectAsync(
        Project project,
        Guid userId,
        CancellationToken ct = default)
    {
        pathResolver.SetUserId(userId);
        var localPath = await pathResolver.ResolveAsync(project, ct);
        logger.LogInformation(
            "Downloading project {Name} from {DropboxPath} to {LocalPath}",
            project.Name, project.DropboxPath, localPath);
        var client = await clientFactory.CreateForUserAsync(userId, ct);

        var files = await client.ListFilesAsync(project.DropboxPath, ct);
        logger.LogInformation("Downloading {Count} files for project {Name}", files.Count, project.Name);
        progressTracker.SetTotalFiles(project.Id, files.Count);

        foreach (var file in files)
        {
            var localFilePath = BuildLocalFilePath(project.DropboxPath, localPath, file.Path);

            await client.DownloadFileAsync(file.Path, localFilePath, ct);
            progressTracker.IncrementFileDownloaded(project.Id);
        }

        logger.LogInformation("Downloaded project {Name} successfully", project.Name);
        return localPath;
    }

    public async Task<(IReadOnlyList<DropboxChangedEntry> Entries, string NewCursor)> ListChangedEntriesAsync(
        Guid userId,
        string cursor,
        CancellationToken ct = default)
    {
        var client = await clientFactory.CreateForUserAsync(userId, ct);
        var (entries, newCursor) = await client.ListChangedEntriesAsync(cursor, ct);
        return (entries, newCursor);
    }

    public async Task<(IReadOnlyList<DropboxChangedEntry> Entries, string InitialCursor)> ListAllEntriesWithCursorAsync(
        Guid userId,
        string dropboxPath,
        CancellationToken ct = default)
    {
        var client = await clientFactory.CreateForUserAsync(userId, ct);
        var (entries, cursor) = await client.ListAllEntriesWithCursorAsync(dropboxPath, ct);
        return (entries, cursor);
    }

    public async Task<string> DownloadChangedEntriesAsync(
        Project project,
        Guid userId,
        IReadOnlyList<DropboxChangedEntry> entries,
        CancellationToken ct = default)
    {
        pathResolver.SetUserId(userId);
        var localPath = await pathResolver.ResolveAsync(project, ct);
        var client = await clientFactory.CreateForUserAsync(userId, ct);

        var fileEntries = entries
            .Where(e => e.EntryType != DropboxEntryType.Deleted)
            .ToList();

        progressTracker.SetTotalFiles(project.Id, fileEntries.Count);

        foreach (var entry in fileEntries)
        {
            var localFilePath = BuildLocalFilePath(project.DropboxPath, localPath, entry.Path);
            await client.DownloadFileAsync(entry.Path, localFilePath, ct);
            progressTracker.IncrementFileDownloaded(project.Id);
        }

        return localPath;
    }

    private static string BuildLocalFilePath(string dropboxRootPath, string localRootPath, string dropboxFilePath)
    {
        var relativePath = dropboxFilePath.StartsWith(dropboxRootPath, StringComparison.OrdinalIgnoreCase)
            ? dropboxFilePath[dropboxRootPath.Length..].TrimStart('/')
            : dropboxFilePath.TrimStart('/');

        return Path.Combine(localRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

