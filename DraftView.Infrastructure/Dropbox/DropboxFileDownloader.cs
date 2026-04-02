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
        ScrivenerProject project,
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
            var relativePath  = file.Path[project.DropboxPath.Length..].TrimStart('/');
            var localFilePath = Path.Combine(localPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            await client.DownloadFileAsync(file.Path, localFilePath, ct);
            progressTracker.IncrementFileDownloaded(project.Id);
        }

        logger.LogInformation("Downloaded project {Name} successfully", project.Name);
        return localPath;
    }
}

