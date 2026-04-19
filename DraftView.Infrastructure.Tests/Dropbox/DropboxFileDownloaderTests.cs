using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Services;
using DraftView.Infrastructure.Dropbox;
using Microsoft.Extensions.Logging;
using Moq;

namespace DraftView.Infrastructure.Tests.Dropbox;

public class DropboxFileDownloaderTests
{
    private readonly Mock<IDropboxClientFactory> _clientFactory = new();
    private readonly Mock<ILocalPathResolver> _pathResolver = new();
    private readonly Mock<ISyncProgressTracker> _progressTracker = new();
    private readonly Mock<ILogger<DropboxFileDownloader>> _logger = new();
    private readonly Mock<IDropboxClient> _client = new();

    private DropboxFileDownloader CreateSut()
    {
        _clientFactory.Setup(f => f.CreateForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_client.Object);

        return new DropboxFileDownloader(
            _clientFactory.Object,
            _pathResolver.Object,
            _progressTracker.Object,
            _logger.Object);
    }

    [Fact]
    public async Task ListChangedEntriesAsync_ReturnsParsedEntries_ForModifiedFiles()
    {
        var userId = Guid.NewGuid();
        var expected = new List<DropboxChangedEntry>
        {
            new("/apps/test/files/data/SCEN-001/content.rtf", DropboxEntryType.Modified, "hash-1")
        };

        _client.Setup(c => c.ListChangedEntriesAsync("cursor-1", default))
            .ReturnsAsync((expected, "cursor-2"));

        var sut = CreateSut();

        var (entries, newCursor) = await sut.ListChangedEntriesAsync(userId, "cursor-1");

        Assert.Single(entries);
        Assert.Equal(DropboxEntryType.Modified, entries[0].EntryType);
        Assert.Equal("cursor-2", newCursor);
    }

    [Fact]
    public async Task ListChangedEntriesAsync_ReturnsParsedEntries_ForDeletedFiles()
    {
        var userId = Guid.NewGuid();
        var expected = new List<DropboxChangedEntry>
        {
            new("/apps/test/files/data/SCEN-002/content.rtf", DropboxEntryType.Deleted, null)
        };

        _client.Setup(c => c.ListChangedEntriesAsync("cursor-1", default))
            .ReturnsAsync((expected, "cursor-2"));

        var sut = CreateSut();

        var (entries, _) = await sut.ListChangedEntriesAsync(userId, "cursor-1");

        Assert.Single(entries);
        Assert.Equal(DropboxEntryType.Deleted, entries[0].EntryType);
    }

    [Fact]
    public async Task ListChangedEntriesAsync_HandlesMultiplePages()
    {
        var userId = Guid.NewGuid();
        var expected = new List<DropboxChangedEntry>
        {
            new("/apps/test/files/data/SCEN-001/content.rtf", DropboxEntryType.Modified, "hash-1"),
            new("/apps/test/files/data/SCEN-002/content.rtf", DropboxEntryType.Modified, "hash-2")
        };

        _client.Setup(c => c.ListChangedEntriesAsync("cursor-1", default))
            .ReturnsAsync((expected, "cursor-2"));

        var sut = CreateSut();

        var (entries, _) = await sut.ListChangedEntriesAsync(userId, "cursor-1");

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task ListAllEntriesWithCursorAsync_ReturnsAllEntriesAndCursor()
    {
        var userId = Guid.NewGuid();
        var expected = new List<DropboxChangedEntry>
        {
            new("/apps/test/project.scrivx", DropboxEntryType.Added, "hash-scrivx"),
            new("/apps/test/files/data/SCEN-003/content.rtf", DropboxEntryType.Added, "hash-3")
        };

        _client.Setup(c => c.ListAllEntriesWithCursorAsync("/apps/test", default))
            .ReturnsAsync((expected, "cursor-initial"));

        var sut = CreateSut();

        var (entries, cursor) = await sut.ListAllEntriesWithCursorAsync(userId, "/apps/test");

        Assert.Equal(2, entries.Count);
        Assert.Equal("cursor-initial", cursor);
    }

    [Fact]
    public async Task DownloadChangedEntriesAsync_DownloadsOnlyAddedOrModifiedEntries()
    {
        var project = Project.Create("Test", "/apps/test", Guid.NewGuid());
        var entries = new List<DropboxChangedEntry>
        {
            new("/apps/test/files/data/A/content.rtf", DropboxEntryType.Added, "h1"),
            new("/apps/test/files/data/B/content.rtf", DropboxEntryType.Modified, "h2"),
            new("/apps/test/files/data/C/content.rtf", DropboxEntryType.Deleted, null)
        };

        _pathResolver.Setup(p => p.ResolveAsync(project, default)).ReturnsAsync("C:/cache/test");
        var sut = CreateSut();

        await sut.DownloadChangedEntriesAsync(project, project.AuthorId, entries);

        _client.Verify(c => c.DownloadFileAsync("/apps/test/files/data/A/content.rtf", It.IsAny<string>(), default), Times.Once);
        _client.Verify(c => c.DownloadFileAsync("/apps/test/files/data/B/content.rtf", It.IsAny<string>(), default), Times.Once);
        _client.Verify(c => c.DownloadFileAsync("/apps/test/files/data/C/content.rtf", It.IsAny<string>(), default), Times.Never);
    }
}
