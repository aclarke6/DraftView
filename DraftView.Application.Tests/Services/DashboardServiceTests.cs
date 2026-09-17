using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for DashboardService dashboard-summary and notification methods.
/// Covers: published chapter progress hierarchy, notification retrieval/filtering,
/// and notification dismissal flows.
/// Excludes: controller/view rendering, which is covered in Web layer tests.
/// </summary>
public class DashboardServiceTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();

    private readonly Mock<ISectionRepository>              _sectionRepo      = new();
    private readonly Mock<IUserRepository>                 _userRepo         = new();
    private readonly Mock<IEmailDeliveryLogRepository>     _logRepo          = new();
    private readonly Mock<ICommentRepository>              _commentRepo      = new();
    private readonly Mock<IReadEventRepository>            _readEventRepo    = new();
    private readonly Mock<IReaderAccessRepository>         _readerAccessRepo = new();
    private readonly Mock<IAuthorNotificationRepository>   _notificationRepo = new();
    private readonly Mock<IUnitOfWork>                     _unitOfWork       = new();

    private DashboardService CreateSut() => new(
        _sectionRepo.Object,
        _userRepo.Object,
        _logRepo.Object,
        _commentRepo.Object,
        _readEventRepo.Object,
        _readerAccessRepo.Object,
        _notificationRepo.Object,
        _unitOfWork.Object);

    // -----------------------------------------------------------------------
    // Existing tests — must remain GREEN
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetProjectOverviewAsync_ReturnsSections()
    {
        var projectId = Guid.NewGuid();
        var section   = Section.CreateDocument(projectId, "UUID-1", "Scene 1",
            null, 0, "<p>x</p>", "h", "First Draft");
        section.PublishAsPartOfChapter("h");
        var sut = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section> { section });

        var result = await sut.GetProjectOverviewAsync(projectId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetReaderSummaryAsync_ReturnsBetaReaders()
    {
        var reader = User.Create("reader@example.com", "Reader", Role.BetaReader);
        reader.Activate();
        var sut = CreateSut();

        _userRepo.Setup(r => r.GetAllBetaReadersAsync(default))
            .ReturnsAsync(new List<User> { reader });

        var result = await sut.GetReaderSummaryAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetEmailHealthSummaryAsync_ReturnsFailedLogs()
    {
        var log = EmailDeliveryLog.Create(Guid.NewGuid(), "test@example.com",
            EmailType.Invitation, null);
        log.RecordAttempt(false, "Timeout.");
        log.MarkFailed();
        var sut = CreateSut();

        _logRepo.Setup(r => r.GetFailedAsync(default))
            .ReturnsAsync(new List<EmailDeliveryLog> { log });

        var result = await sut.GetEmailHealthSummaryAsync();

        Assert.Single(result);
        Assert.Equal(EmailStatus.Failed, result[0].Status);
    }

    [Fact]
    public async Task GetPublishedChapterProgressAsync_WithStructuralParents_GroupsChaptersAndAggregatesReaderActivity()
    {
        var projectId  = Guid.NewGuid();
        var partA      = Section.CreateFolder(projectId, "part-a", "Part A", null, 0);
        var emptyPart  = Section.CreateFolder(projectId, "part-empty", "Empty Part", null, 1);
        var chapterOne = Section.CreateFolder(projectId, "chapter-1", "Chapter 1", partA.Id, 0);
        var chapterTwo = Section.CreateFolder(projectId, "chapter-2", "Chapter 2", null, 2);
        var sceneOne   = Section.CreateDocument(projectId, "scene-1", "Scene 1", chapterOne.Id, 0, "<p>x</p>", "hash-1", "Done");

        chapterOne.MarkAsPublishedContainer();
        chapterTwo.MarkAsPublishedContainer();
        sceneOne.PublishAsPartOfChapter("hash-1");

        var readerAlice = User.Create("alice@example.com", "Alice", Role.BetaReader);
        var readerBen   = User.Create("ben@example.com", "Ben", Role.BetaReader);
        readerAlice.Activate();
        readerBen.Activate();

        var aliceAccess = ReaderAccess.Grant(readerAlice.Id, AuthorId, projectId);
        var benAccess   = ReaderAccess.Grant(readerBen.Id, AuthorId, projectId);
        var readEvent   = ReadEvent.Create(sceneOne.Id, readerAlice.Id);

        var chapterComment = Comment.CreateForImport(
            chapterOne.Id,
            readerBen.Id,
            "Chapter comment",
            Visibility.Public,
            CommentStatus.Done,
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var sceneComment = Comment.CreateForImport(
            sceneOne.Id,
            readerAlice.Id,
            "Scene comment",
            Visibility.Public,
            CommentStatus.New,
            new DateTime(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc));

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync([emptyPart, chapterTwo, sceneOne, partA, chapterOne]);
        _readerAccessRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync([aliceAccess, benAccess]);
        _userRepo.Setup(r => r.GetByIdAsync(readerAlice.Id, default)).ReturnsAsync(readerAlice);
        _userRepo.Setup(r => r.GetByIdAsync(readerBen.Id, default)).ReturnsAsync(readerBen);
        _readEventRepo.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync([readEvent]);
        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(chapterOne.Id, default)).ReturnsAsync([chapterComment]);
        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(sceneOne.Id, default)).ReturnsAsync([sceneComment]);
        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(chapterTwo.Id, default)).ReturnsAsync([]);

        var result = await CreateSut().GetPublishedChapterProgressAsync(projectId, AuthorId);

        Assert.True(result.UsesStructuralGroups);
        Assert.Empty(result.Chapters);
        Assert.Equal(2, result.Groups.Count);

        var grouped = result.Groups[0];
        Assert.Equal("Part A", grouped.Title);
        Assert.False(grouped.IsUngrouped);
        Assert.Equal(1, grouped.ViewedReaderChapterCount);
        Assert.Equal(2, grouped.TotalReaderChapterCount);
        Assert.Equal(2, grouped.CommentCount);
        Assert.Equal(1, grouped.NewCommentCount);
        Assert.Equal(new[] { sceneComment.CreatedAt, readEvent.LastOpenedAt }.Max(), grouped.LatestActivityAt);

        var chapter = Assert.Single(grouped.Chapters);
        Assert.Equal(chapterOne.Id, chapter.Chapter.Id);
        Assert.Equal(1, chapter.ViewedReaderCount);
        Assert.Equal(2, chapter.TotalReaderCount);
        Assert.Equal(readEvent.LastOpenedAt, chapter.LatestViewAt);
        Assert.Equal(2, chapter.CommentCount);
        Assert.Equal(1, chapter.NewCommentCount);
        Assert.NotNull(chapter.LatestComment);
        Assert.Equal(sceneOne.Id, chapter.LatestComment!.SectionId);
        Assert.Equal(sceneComment.Id, chapter.LatestComment.CommentId);
        Assert.Equal("Scene 1", chapter.LatestComment.SectionTitle);

        Assert.Equal(2, chapter.Readers.Count);
        Assert.Equal("Alice", chapter.Readers[0].ReaderName);
        Assert.True(chapter.Readers[0].HasViewed);
        Assert.Equal(readEvent.LastOpenedAt, chapter.Readers[0].LastViewedAt);
        Assert.Equal(1, chapter.Readers[0].CommentCount);
        Assert.Equal("Ben", chapter.Readers[1].ReaderName);
        Assert.False(chapter.Readers[1].HasViewed);
        Assert.Null(chapter.Readers[1].LastViewedAt);
        Assert.Equal(1, chapter.Readers[1].CommentCount);

        var ungrouped = result.Groups[1];
        Assert.Equal("Ungrouped chapters", ungrouped.Title);
        Assert.True(ungrouped.IsUngrouped);
        Assert.Single(ungrouped.Chapters);
        Assert.Equal(chapterTwo.Id, ungrouped.Chapters[0].Chapter.Id);
        Assert.Equal(0, ungrouped.CommentCount);
        Assert.Equal(0, ungrouped.NewCommentCount);
    }

    [Fact]
    public async Task GetPublishedChapterProgressAsync_WithoutStructuralParents_ReturnsChapterRowsWithViewedAndUnviewedReaders()
    {
        var projectId = Guid.NewGuid();
        var chapter   = Section.CreateFolder(projectId, "chapter-1", "Chapter 1", null, 0);
        var scene     = Section.CreateDocument(projectId, "scene-1", "Scene 1", chapter.Id, 0, "<p>x</p>", "hash-1", "Done");

        chapter.MarkAsPublishedContainer();
        scene.PublishAsPartOfChapter("hash-1");

        var readerAlice = User.Create("alice@example.com", "Alice", Role.BetaReader);
        var readerBen   = User.Create("ben@example.com", "Ben", Role.BetaReader);
        readerAlice.Activate();
        readerBen.Activate();

        var aliceAccess = ReaderAccess.Grant(readerAlice.Id, AuthorId, projectId);
        var benAccess   = ReaderAccess.Grant(readerBen.Id, AuthorId, projectId);
        var readEvent   = ReadEvent.Create(scene.Id, readerBen.Id);
        var benComment  = Comment.CreateForImport(
            scene.Id,
            readerBen.Id,
            "Ben comment",
            Visibility.Public,
            CommentStatus.New,
            new DateTime(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc));

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync([chapter, scene]);
        _readerAccessRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync([benAccess, aliceAccess]);
        _userRepo.Setup(r => r.GetByIdAsync(readerAlice.Id, default)).ReturnsAsync(readerAlice);
        _userRepo.Setup(r => r.GetByIdAsync(readerBen.Id, default)).ReturnsAsync(readerBen);
        _readEventRepo.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync([readEvent]);
        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(chapter.Id, default)).ReturnsAsync([]);
        _commentRepo.Setup(r => r.GetAllBySectionIdAsync(scene.Id, default)).ReturnsAsync([benComment]);

        var result = await CreateSut().GetPublishedChapterProgressAsync(projectId, AuthorId);

        Assert.False(result.UsesStructuralGroups);
        Assert.Empty(result.Groups);

        var chapterRow = Assert.Single(result.Chapters);
        Assert.Equal(chapter.Id, chapterRow.Chapter.Id);
        Assert.Equal(1, chapterRow.ViewedReaderCount);
        Assert.Equal(2, chapterRow.TotalReaderCount);
        Assert.Equal(1, chapterRow.CommentCount);
        Assert.Equal(1, chapterRow.NewCommentCount);
        Assert.NotNull(chapterRow.LatestComment);
        Assert.Equal(scene.Id, chapterRow.LatestComment!.SectionId);

        Assert.Equal(2, chapterRow.Readers.Count);
        Assert.Equal("Alice", chapterRow.Readers[0].ReaderName);
        Assert.False(chapterRow.Readers[0].HasViewed);
        Assert.Null(chapterRow.Readers[0].LastViewedAt);
        Assert.Equal(0, chapterRow.Readers[0].CommentCount);

        Assert.Equal("Ben", chapterRow.Readers[1].ReaderName);
        Assert.True(chapterRow.Readers[1].HasViewed);
        Assert.Equal(readEvent.LastOpenedAt, chapterRow.Readers[1].LastViewedAt);
        Assert.Equal(1, chapterRow.Readers[1].CommentCount);
    }

    // -----------------------------------------------------------------------
    // GetNotificationsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetNotificationsAsync_ReturnsNotificationsForAuthor()
    {
        var n = AuthorNotification.Create(
            AuthorId, NotificationEventType.NewComment, "Alice commented", null, null, DateTime.UtcNow);
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAsync(AuthorId, default))
            .ReturnsAsync(new List<AuthorNotification> { n });

        var result = await sut.GetNotificationsAsync(AuthorId);

        Assert.Single(result);
        Assert.Equal("Alice commented", result[0].Title);
    }

    [Fact]
    public async Task GetNotificationsAsync_CallsPruneBeforeReturning()
    {
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAsync(AuthorId, default))
            .ReturnsAsync(new List<AuthorNotification>());

        await sut.GetNotificationsAsync(AuthorId);

        _notificationRepo.Verify(
            r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default),
            Times.Once);
    }

    [Fact]
    public async Task GetNotificationsAsync_DefaultFeed_ExcludesSyncCompletedNotifications()
    {
        var commentNotification = AuthorNotification.Create(
            AuthorId, NotificationEventType.NewComment, "Alice commented", null, null, DateTime.UtcNow);
        var syncNotification = AuthorNotification.Create(
            AuthorId, NotificationEventType.SyncCompleted, "Sync completed for Novel", null, null, DateTime.UtcNow);
        var uploadNotification = AuthorNotification.Create(
            AuthorId, NotificationEventType.ChapterUploaded, "Chapter uploaded", null, null, DateTime.UtcNow);
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAsync(AuthorId, default))
            .ReturnsAsync([commentNotification, syncNotification, uploadNotification]);

        var result = await sut.GetNotificationsAsync(AuthorId);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, n => n.EventType == NotificationEventType.SyncCompleted);
        Assert.Contains(result, n => n.EventType == NotificationEventType.NewComment);
        Assert.Contains(result, n => n.EventType == NotificationEventType.ChapterUploaded);
    }

    // -----------------------------------------------------------------------
    // DismissNotificationAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DismissNotificationAsync_CallsDeleteOnRepo()
    {
        var notifId = Guid.NewGuid();
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.DeleteAsync(notifId, default))
            .Returns(Task.CompletedTask);

        await sut.DismissNotificationAsync(notifId);

        _notificationRepo.Verify(r => r.DeleteAsync(notifId, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // DismissAllNotificationsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DismissAllNotificationsAsync_CallsDeleteAllOnRepo()
    {
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.DeleteAllByAuthorIdAsync(AuthorId, default))
            .Returns(Task.CompletedTask);

        await sut.DismissAllNotificationsAsync(AuthorId);

        _notificationRepo.Verify(r => r.DeleteAllByAuthorIdAsync(AuthorId, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // -----------------------------------------------------------------------
    // GetNotificationsAsync — filter group
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetNotificationsAsync_WithFilterGroup_CallsMultiTypeRepoMethod()
    {
        var n = AuthorNotification.Create(
            AuthorId, NotificationEventType.NewComment, "Alice commented", null, null, DateTime.UtcNow);
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAndTypesAsync(AuthorId, It.IsAny<IReadOnlyList<NotificationEventType>>(), default))
            .ReturnsAsync(new List<AuthorNotification> { n });

        var result = await sut.GetNotificationsAsync(AuthorId, NotificationFilterGroup.Comments);

        Assert.Single(result);
        _notificationRepo.Verify(
            r => r.GetByAuthorIdAndTypesAsync(AuthorId, It.IsAny<IReadOnlyList<NotificationEventType>>(), default),
            Times.Once);
        _notificationRepo.Verify(r => r.GetByAuthorIdAsync(AuthorId, default), Times.Never);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReadersGroup_IncludesReadNewSceneType()
    {
        var sut = CreateSut();
        IReadOnlyList<NotificationEventType>? capturedTypes = null;

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAndTypesAsync(AuthorId, It.IsAny<IReadOnlyList<NotificationEventType>>(), default))
            .Callback<Guid, IReadOnlyList<NotificationEventType>, CancellationToken>((_, types, _) => capturedTypes = types)
            .ReturnsAsync(new List<AuthorNotification>());

        await sut.GetNotificationsAsync(AuthorId, NotificationFilterGroup.Readers);

        Assert.NotNull(capturedTypes);
        Assert.Contains(NotificationEventType.ReaderReadNewScene, capturedTypes);
        Assert.Contains(NotificationEventType.ReaderJoined, capturedTypes);
    }

    [Fact]
    public async Task GetNotificationsAsync_WithNullGroup_CallsUnfilteredRepoMethod()
    {
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.PruneOlderThanAsync(AuthorId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);
        _notificationRepo
            .Setup(r => r.GetByAuthorIdAsync(AuthorId, default))
            .ReturnsAsync(new List<AuthorNotification>());

        await sut.GetNotificationsAsync(AuthorId, null);

        _notificationRepo.Verify(r => r.GetByAuthorIdAsync(AuthorId, default), Times.Once);
        _notificationRepo.Verify(
            r => r.GetByAuthorIdAndTypesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<NotificationEventType>>(), default),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // DismissNotificationsByTypeAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DismissNotificationsByTypeAsync_WithSpecificType_CallsDeleteByType()
    {
        var type = NotificationEventType.NewComment;
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.DeleteByAuthorIdAndTypeAsync(AuthorId, type, default))
            .Returns(Task.CompletedTask);

        await sut.DismissNotificationsByTypeAsync(AuthorId, type);

        _notificationRepo.Verify(r => r.DeleteByAuthorIdAndTypeAsync(AuthorId, type, default), Times.Once);
        _notificationRepo.Verify(r => r.DeleteAllByAuthorIdAsync(AuthorId, default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DismissNotificationsByTypeAsync_WithNullType_CallsDeleteAll()
    {
        var sut = CreateSut();

        _notificationRepo
            .Setup(r => r.DeleteAllByAuthorIdAsync(AuthorId, default))
            .Returns(Task.CompletedTask);

        await sut.DismissNotificationsByTypeAsync(AuthorId, null);

        _notificationRepo.Verify(r => r.DeleteAllByAuthorIdAsync(AuthorId, default), Times.Once);
        _notificationRepo.Verify(
            r => r.DeleteByAuthorIdAndTypeAsync(It.IsAny<Guid>(), It.IsAny<NotificationEventType>(), default),
            Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
