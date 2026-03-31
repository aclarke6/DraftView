using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public sealed class DiscoveryServiceOptions
{
    public string LocalCachePath { get; init; } = string.Empty;
}

public class ScrivenerProjectDiscoveryService(
    IDropboxClient dropboxClient,
    IScrivenerProjectParser parser,
    IScrivenerProjectRepository projectRepo,
    DiscoveryServiceOptions options) : IScrivenerProjectDiscoveryService
{
    public async Task<IReadOnlyList<DiscoveredProject>> DiscoverAsync(
        CancellationToken ct = default)
    {
        var existing     = await projectRepo.GetAllAsync(ct);
        var existingKeys = existing
            .Where(p => p.ScrivenerRootUuid is not null && !p.IsSoftDeleted)
            .Select(p => p.ScrivenerRootUuid!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scrivFolders = await dropboxClient.ListScrivFoldersAsync(ct);
        var discovered   = new List<DiscoveredProject>();

        foreach (var folder in scrivFolders)
        {
            try
            {
                var projects = await DiscoverFromVaultAsync(folder, existingKeys, ct);
                discovered.AddRange(projects);
            }
            catch
            {
                // Skip vaults that fail to parse
            }
        }

        return discovered;
    }

    private async Task<IReadOnlyList<DiscoveredProject>> DiscoverFromVaultAsync(
        DropboxFileInfo folder,
        HashSet<string> existingKeys,
        CancellationToken ct)
    {
        var vaultName    = Path.GetFileNameWithoutExtension(folder.Name);
        var localCacheDir = Path.Combine(options.LocalCachePath, "_discovery", vaultName);
        Directory.CreateDirectory(localCacheDir);

        var files  = await dropboxClient.ListFilesAsync(folder.Path, ct);
        var scrivx = files.FirstOrDefault(f =>
            f.Name.EndsWith(".scrivx", StringComparison.OrdinalIgnoreCase) &&
            !f.Path.Contains("/Files/") &&
            !f.Path.Contains("\\Files\\"));

        if (scrivx is null) return Array.Empty<DiscoveredProject>();

        var localScrivxPath = Path.Combine(localCacheDir, scrivx.Name);
        await dropboxClient.DownloadFileAsync(scrivx.Path, localScrivxPath, ct);

        var parsed = parser.Parse(localScrivxPath);
        if (parsed.ManuscriptRoot is null) return Array.Empty<DiscoveredProject>();

        return ApplyBookSplitDetection(parsed, folder, vaultName, existingKeys);
    }

    private static IReadOnlyList<DiscoveredProject> ApplyBookSplitDetection(
        ParsedProject parsed,
        DropboxFileInfo folder,
        string vaultName,
        HashSet<string> existingKeys)
    {
        var root     = parsed.ManuscriptRoot!;
        var children = root.Children;

        var isBookSplit = children.Count > 1 &&
                          children.All(c => c.NodeType == ParsedNodeType.Folder);

        if (isBookSplit)
        {
            return children.Select(book => new DiscoveredProject
            {
                Name              = book.Title,
                DropboxPath       = folder.Path,
                ScrivenerRootUuid = book.Uuid,
                AlreadyAdded      = existingKeys.Contains(book.Uuid)
            }).ToList();
        }

        return new[]
        {
            new DiscoveredProject
            {
                Name              = vaultName,
                DropboxPath       = folder.Path,
                ScrivenerRootUuid = root.Uuid,
                AlreadyAdded      = existingKeys.Contains(root.Uuid)
            }
        };
    }
}

