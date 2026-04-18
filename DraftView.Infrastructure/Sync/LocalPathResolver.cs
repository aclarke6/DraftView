using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraftView.Infrastructure.Sync;

public class LocalPathResolver : ILocalPathResolver
{
    private readonly string _localCachePath;
    private readonly IPlatformPathService _platformPathService;
    private readonly ILogger<LocalPathResolver> _logger;
    private string? _resolvedCacheRoot;
    private Guid _userId;

    public LocalPathResolver(string localCachePath)
        : this(localCachePath, new PlatformPathService(), NullLogger<LocalPathResolver>.Instance)
    {
    }

    public LocalPathResolver(
        string localCachePath,
        IPlatformPathService platformPathService,
        ILogger<LocalPathResolver> logger)
    {
        _localCachePath = localCachePath;
        _platformPathService = platformPathService;
        _logger = logger;
    }

    public void SetUserId(Guid userId) => _userId = userId;

    public Task<string> ResolveAsync(Project project, CancellationToken ct = default)
    {
        var cacheRoot = ResolveCacheRoot();
        var userCachePath = _userId != Guid.Empty
            ? Path.Combine(cacheRoot, _userId.ToString())
            : cacheRoot;
        var projectDirectoryName = Path.GetFileName(project.DropboxPath.TrimEnd('/').TrimEnd('\\'));

        if (string.IsNullOrWhiteSpace(projectDirectoryName))
            throw new InvalidOperationException(
                $"Could not resolve a local cache directory name from Dropbox path '{project.DropboxPath}'.");

        var basePath = Path.Combine(userCachePath, projectDirectoryName);
        Directory.CreateDirectory(basePath);

        return Task.FromResult(basePath);
    }

    public async Task<string> ResolveScrivxAsync(Project project, CancellationToken ct = default)
    {
        var vaultPath = await ResolveAsync(project, ct);

        // Search for the .scrivx file (case-insensitive on Linux)
        if (Directory.Exists(vaultPath))
        {
            var scrivxFile = Directory.GetFiles(vaultPath, "*.scrivx", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (scrivxFile is not null)
                return scrivxFile;
        }

        // Fallback to constructed path
        var dirName = Path.GetFileNameWithoutExtension(vaultPath);
        return Path.Combine(vaultPath, dirName + ".scrivx");
    }

    private string ResolveCacheRoot()
    {
        if (_resolvedCacheRoot is not null)
            return _resolvedCacheRoot;

        var cacheRoot = string.IsNullOrWhiteSpace(_localCachePath)
            ? ResolveDefaultCacheRoot()
            : NormalizeConfiguredCacheRoot(_localCachePath);

        Directory.CreateDirectory(cacheRoot);
        _logger.LogInformation("Resolved local cache root to {LocalCacheRoot}", cacheRoot);
        _resolvedCacheRoot = cacheRoot;
        return cacheRoot;
    }

    private string ResolveDefaultCacheRoot()
    {
        if (_platformPathService.IsWindows)
        {
            var localApplicationData = _platformPathService.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return CombineCacheRoot(localApplicationData, "Windows LocalApplicationData");
        }

        var userProfile = _platformPathService.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (_platformPathService.IsMacOS)
            return CombineCacheRoot(userProfile, "macOS user profile", "Library", "Application Support");

        if (_platformPathService.IsLinux)
            return CombineCacheRoot(userProfile, "Linux user profile", ".local", "share");

        throw new InvalidOperationException("Unable to resolve a local cache path for the current operating system.");
    }

    private static string NormalizeConfiguredCacheRoot(string configuredLocalCachePath)
    {
        try
        {
            return Path.GetFullPath(configuredLocalCachePath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException(
                "Configured DraftView:LocalCachePath is invalid.",
                ex);
        }
    }

    private static string CombineCacheRoot(
        string basePath,
        string sourceDescription,
        params string[] additionalSegments)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new InvalidOperationException(
                $"Unable to resolve local cache path because {sourceDescription} is unavailable.");

        var segments = new[] { basePath }
            .Concat(additionalSegments)
            .Concat(["DraftView", "Cache"])
            .ToArray();

        return Path.GetFullPath(Path.Combine(segments));
    }
}
