using DraftView.Domain.Entities;
using DraftView.Infrastructure.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraftView.Infrastructure.Tests.Sync;

public class LocalPathResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenConfiguredCachePathProvided_UsesConfiguredPath()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var authorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create("Novel", "/Apps/Scrivener/Novel.scriv", authorId);
        var platformPathService = FakePlatformPathService.CreateLinux(rootPath);
        var resolver = new LocalPathResolver(rootPath, platformPathService, NullLogger<LocalPathResolver>.Instance);

        try
        {
            resolver.SetUserId(userId);

            var resolvedPath = await resolver.ResolveAsync(project);

            var expectedPath = Path.Combine(rootPath, userId.ToString(), "Novel.scriv");
            Assert.Equal(expectedPath, resolvedPath);
            Assert.True(Directory.Exists(resolvedPath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, recursive: true);
        }
    }

    [Theory]
    [InlineData(TestPlatform.Windows)]
    [InlineData(TestPlatform.MacOS)]
    [InlineData(TestPlatform.Linux)]
    public async Task ResolveAsync_WhenCachePathNotConfigured_UsesPlatformDefault(TestPlatform platform)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var authorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var homePath = Path.Combine(tempRoot, "home");
        var localAppDataPath = Path.Combine(tempRoot, "local-app-data");
        var project = Project.Create("Novel", "/Apps/Scrivener/Novel.scriv", authorId);
        var platformPathService = FakePlatformPathService.Create(platform, homePath, localAppDataPath);
        var resolver = new LocalPathResolver(string.Empty, platformPathService, NullLogger<LocalPathResolver>.Instance);

        try
        {
            resolver.SetUserId(userId);

            var resolvedPath = await resolver.ResolveAsync(project);

            var expectedRoot = platform switch
            {
                TestPlatform.Windows => Path.Combine(localAppDataPath, "DraftView", "Cache"),
                TestPlatform.MacOS => Path.Combine(homePath, "Library", "Application Support", "DraftView", "Cache"),
                TestPlatform.Linux => Path.Combine(homePath, ".local", "share", "DraftView", "Cache"),
                _ => throw new InvalidOperationException("Unsupported test platform.")
            };

            var expectedPath = Path.Combine(expectedRoot, userId.ToString(), "Novel.scriv");
            Assert.Equal(expectedPath, resolvedPath);
            Assert.True(Directory.Exists(resolvedPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenConfiguredPathIsInvalid_ThrowsClearException()
    {
        var project = Project.Create("Novel", "/Apps/Scrivener/Novel.scriv", Guid.NewGuid());
        var platformPathService = FakePlatformPathService.CreateLinux(Path.GetTempPath());
        var resolver = new LocalPathResolver("\0", platformPathService, NullLogger<LocalPathResolver>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(project));

        Assert.Contains("DraftView:LocalCachePath", exception.Message, StringComparison.Ordinal);
    }

    public enum TestPlatform
    {
        Windows,
        MacOS,
        Linux
    }

    private sealed class FakePlatformPathService : IPlatformPathService
    {
        private readonly TestPlatform _platform;
        private readonly string _homePath;
        private readonly string _localAppDataPath;

        private FakePlatformPathService(TestPlatform platform, string homePath, string localAppDataPath)
        {
            _platform = platform;
            _homePath = homePath;
            _localAppDataPath = localAppDataPath;
        }

        public bool IsWindows => _platform == TestPlatform.Windows;
        public bool IsMacOS => _platform == TestPlatform.MacOS;
        public bool IsLinux => _platform == TestPlatform.Linux;

        public string GetFolderPath(Environment.SpecialFolder folder)
        {
            return folder switch
            {
                Environment.SpecialFolder.UserProfile => _homePath,
                Environment.SpecialFolder.LocalApplicationData => _localAppDataPath,
                _ => string.Empty
            };
        }

        public static FakePlatformPathService Create(
            TestPlatform platform,
            string homePath,
            string localAppDataPath)
        {
            return new FakePlatformPathService(platform, homePath, localAppDataPath);
        }

        public static FakePlatformPathService CreateLinux(string homePath)
        {
            return new FakePlatformPathService(TestPlatform.Linux, homePath, Path.Combine(homePath, ".local-app-data"));
        }
    }
}