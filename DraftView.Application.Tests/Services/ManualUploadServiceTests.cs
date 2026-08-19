using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ManualUploadService.
/// Covers: file upload, paste upload, reorder, replace, inline edit, delete,
///         version history clear, Scrivener project rejection, AuthorNotification emission.
/// Excludes: file parsing internals (Infrastructure.Tests/Parsing),
///           EF persistence (Infrastructure.Tests/Persistence).
/// </summary>
public class ManualUploadServiceTests
{
    private static readonly Guid AuthorId  = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    private readonly Mock<IProjectRepository>              _projectRepo      = new();
    private readonly Mock<IManualChapterRepository>        _chapterRepo      = new();
    private readonly Mock<IManualChapterVersionRepository> _versionRepo      = new();
    private readonly Mock<IAuthorNotificationRepository>   _notificationRepo = new();
    private readonly Mock<IChapterFileParserResolver>      _parserResolver   = new();
    private readonly Mock<IChapterFileParser>              _parser           = new();
    private readonly Mock<IUnitOfWork>                     _unitOfWork       = new();

    private ManualUploadService CreateSut() => new(
        _projectRepo.Object,
        _chapterRepo.Object,
        _versionRepo.Object,
        _notificationRepo.Object,
        _parserResolver.Object,
        _unitOfWork.Object);

    private static Project BuildManualProject()    => Project.CreateManual("Test Book", AuthorId);
    private static Project BuildScrivenerProject() => Project.Create("Test Book", "/dropbox/test", AuthorId);

    // -----------------------------------------------------------------------
    // UploadChapterAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UploadChapterAsync_ManualProject_CreatesChapterAndNotification()
    {
        var project = BuildManualProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _chapterRepo.Setup(r => r.CountByProjectAsync(ProjectId, AuthorId, default)).ReturnsAsync(0);
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default)).ReturnsAsync("parsed content");
        _parserResolver.Setup(r => r.Resolve(".txt")).Returns(_parser.Object);

        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[10]);

        var result = await sut.UploadChapterAsync(
            ProjectId, AuthorId, "Chapter 1", stream, "chapter.txt");

        Assert.NotNull(result);
        Assert.Equal("Chapter 1", result.Title);
        Assert.Equal("parsed content", result.RawContent);
        Assert.Equal("chapter.txt", result.OriginalFileName);
        _chapterRepo.Verify(r => r.AddAsync(It.IsAny<ManualChapter>(), default), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<AuthorNotification>(n => n.EventType == NotificationEventType.ChapterUploaded),
            default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UploadChapterAsync_ScrivenerProject_ThrowsUnauthorisedOperation()
    {
        var project = BuildScrivenerProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[10]);

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.UploadChapterAsync(ProjectId, AuthorId, "Chapter 1", stream, "chapter.txt"));
    }

    [Fact]
    public async Task UploadChapterAsync_ProjectNotFound_ThrowsEntityNotFound()
    {
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync((Project?)null);

        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[10]);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.UploadChapterAsync(ProjectId, AuthorId, "Chapter 1", stream, "chapter.txt"));
    }

    [Fact]
    public async Task UploadChapterAsync_SortOrderEqualsCurrentChapterCount()
    {
        var project = BuildManualProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _chapterRepo.Setup(r => r.CountByProjectAsync(ProjectId, AuthorId, default)).ReturnsAsync(3);
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default)).ReturnsAsync("content");
        _parserResolver.Setup(r => r.Resolve(".txt")).Returns(_parser.Object);

        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[10]);

        var result = await sut.UploadChapterAsync(ProjectId, AuthorId, "Ch4", stream, "ch.txt");

        Assert.Equal(3, result.SortOrder);
    }

    // -----------------------------------------------------------------------
    // UploadChapterFromPasteAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UploadChapterFromPasteAsync_ManualProject_StoresContentWithNullFileName()
    {
        var project = BuildManualProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);
        _chapterRepo.Setup(r => r.CountByProjectAsync(ProjectId, AuthorId, default)).ReturnsAsync(0);

        var sut = CreateSut();

        var result = await sut.UploadChapterFromPasteAsync(
            ProjectId, AuthorId, "Pasted Chapter", "# My content\n\nHello world.");

        Assert.NotNull(result);
        Assert.Equal("Pasted Chapter", result.Title);
        Assert.Equal("# My content\n\nHello world.", result.RawContent);
        Assert.Null(result.OriginalFileName);
        _chapterRepo.Verify(r => r.AddAsync(It.IsAny<ManualChapter>(), default), Times.Once);
        _notificationRepo.Verify(r => r.AddAsync(
            It.Is<AuthorNotification>(n => n.EventType == NotificationEventType.ChapterUploaded),
            default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UploadChapterFromPasteAsync_ScrivenerProject_ThrowsUnauthorisedOperation()
    {
        var project = BuildScrivenerProject();
        _projectRepo.Setup(r => r.GetByIdAsync(ProjectId, default)).ReturnsAsync(project);

        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.UploadChapterFromPasteAsync(ProjectId, AuthorId, "Chapter", "content"));
    }

    // -----------------------------------------------------------------------
    // ReorderChaptersAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReorderChaptersAsync_UpdatesSortOrderForEachChapter()
    {
        var ch1 = ManualChapter.Create(AuthorId, ProjectId, "Ch1", 0, "c1", null, DateTime.UtcNow);
        var ch2 = ManualChapter.Create(AuthorId, ProjectId, "Ch2", 1, "c2", null, DateTime.UtcNow);

        _chapterRepo.Setup(r => r.GetByProjectAsync(ProjectId, AuthorId, default))
            .ReturnsAsync(new List<ManualChapter> { ch1, ch2 });

        var sut = CreateSut();
        // Swap: ch2 first (SortOrder 0), ch1 second (SortOrder 1)
        await sut.ReorderChaptersAsync(ProjectId, AuthorId, new List<Guid> { ch2.Id, ch1.Id });

        Assert.Equal(0, ch2.SortOrder);
        Assert.Equal(1, ch1.SortOrder);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // DeleteChapterAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteChapterAsync_CallsDeleteOnRepository()
    {
        var chapterId = Guid.NewGuid();
        var sut       = CreateSut();

        await sut.DeleteChapterAsync(chapterId, AuthorId);

        _chapterRepo.Verify(r => r.DeleteAsync(chapterId, AuthorId, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // ReplaceChapterAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReplaceChapterAsync_CreatesVersionSnapshotThenUpdatesContent()
    {
        var chapterId = Guid.NewGuid();
        var chapter   = ManualChapter.Create(AuthorId, ProjectId, "Chapter 1", 0, "old content", "old.txt", DateTime.UtcNow);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, AuthorId, default)).ReturnsAsync(chapter);
        _versionRepo.Setup(r => r.GetNextVersionNumberAsync(chapterId, default)).ReturnsAsync(1);
        _parser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), default)).ReturnsAsync("new content");
        _parserResolver.Setup(r => r.Resolve(".txt")).Returns(_parser.Object);

        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[10]);

        var result = await sut.ReplaceChapterAsync(chapterId, AuthorId, stream, "new.txt");

        // Snapshot created from old content before overwrite
        _versionRepo.Verify(r => r.AddAsync(
            It.Is<ManualChapterVersion>(v =>
                v.RawContent == "old content" &&
                v.VersionNumber == 1),
            default), Times.Once);
        Assert.Equal("new content", result.RawContent);
        _chapterRepo.Verify(r => r.UpdateAsync(chapter, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ReplaceChapterAsync_ChapterNotFound_ThrowsEntityNotFound()
    {
        var chapterId = Guid.NewGuid();
        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, AuthorId, default))
            .ReturnsAsync((ManualChapter?)null);

        var sut = CreateSut();
        using var stream = new MemoryStream(new byte[10]);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.ReplaceChapterAsync(chapterId, AuthorId, stream, "new.txt"));
    }

    // -----------------------------------------------------------------------
    // EditChapterAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EditChapterAsync_CreatesInlineEditVersionSnapshotAndUpdatesContent()
    {
        var chapterId = Guid.NewGuid();
        var chapter   = ManualChapter.Create(AuthorId, ProjectId, "Chapter 1", 0, "original content", null, DateTime.UtcNow);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, AuthorId, default)).ReturnsAsync(chapter);
        _versionRepo.Setup(r => r.GetNextVersionNumberAsync(chapterId, default)).ReturnsAsync(2);

        var sut = CreateSut();

        var result = await sut.EditChapterAsync(chapterId, AuthorId, "edited content");

        _versionRepo.Verify(r => r.AddAsync(
            It.Is<ManualChapterVersion>(v =>
                v.SnapshotReason == SnapshotReason.InlineEdit &&
                v.RawContent == "original content" &&
                v.VersionNumber == 2),
            default), Times.Once);
        Assert.Equal("edited content", result.RawContent);
        _chapterRepo.Verify(r => r.UpdateAsync(chapter, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task EditChapterAsync_ChapterNotFound_ThrowsEntityNotFound()
    {
        var chapterId = Guid.NewGuid();
        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, AuthorId, default))
            .ReturnsAsync((ManualChapter?)null);

        var sut = CreateSut();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.EditChapterAsync(chapterId, AuthorId, "new content"));
    }

    // -----------------------------------------------------------------------
    // ClearVersionHistoryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClearVersionHistoryAsync_HardDeletesAllVersionsLeavingChapterIntact()
    {
        var chapterId = Guid.NewGuid();
        var chapter   = ManualChapter.Create(AuthorId, ProjectId, "Chapter 1", 0, "live content", null, DateTime.UtcNow);

        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, AuthorId, default)).ReturnsAsync(chapter);

        var sut = CreateSut();

        await sut.ClearVersionHistoryAsync(chapterId, AuthorId);

        _versionRepo.Verify(r => r.ClearForChapterAsync(chapterId, default), Times.Once);
        _chapterRepo.Verify(r => r.UpdateAsync(It.IsAny<ManualChapter>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ClearVersionHistoryAsync_ChapterNotFound_ThrowsEntityNotFound()
    {
        var chapterId = Guid.NewGuid();
        _chapterRepo.Setup(r => r.GetByIdAsync(chapterId, AuthorId, default))
            .ReturnsAsync((ManualChapter?)null);

        var sut = CreateSut();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.ClearVersionHistoryAsync(chapterId, AuthorId));
    }
}
