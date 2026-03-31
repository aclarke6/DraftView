using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Sync;

public class LocalPathResolver : ILocalPathResolver
{
    private readonly string _localCachePath;

    public LocalPathResolver(string localCachePath)
    {
        _localCachePath = localCachePath;
    }

    public Task<string> ResolveAsync(ScrivenerProject project, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(_localCachePath)
            ? project.DropboxPath
            : Path.Combine(
                _localCachePath,
                Path.GetFileName(project.DropboxPath.TrimEnd('/').TrimEnd('\\')));

        return Task.FromResult(path);
    }

    public async Task<string> ResolveScrivxAsync(ScrivenerProject project, CancellationToken ct = default)
    {
        var vaultPath = await ResolveAsync(project, ct);
        var dirName   = Path.GetFileNameWithoutExtension(vaultPath);
        return Path.Combine(vaultPath, dirName + ".scrivx");
    }
}
