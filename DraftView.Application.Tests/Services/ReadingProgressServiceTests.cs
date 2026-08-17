using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ReadingProgressService orchestration.
/// Covers: read-event creation, progress queries, version tracking, banner dismissal,
/// resume-anchor capture, and resume restore resolution.
/// Excludes: controller binding and client-side capture JavaScript.
/// </summary>
public class ReadingProgressServiceTests
{
    private readonly Mock<IReadEventRepository>          _readEventRepo       = new();
    private readonly Mock<ISectionRepository>            _sectionRepo         = new();
    private readonly Mock<IPassageAnchorService>         _passageAnchorService = new();
    private readonly Mock<IUnitOfWork>                   _unitOfWork          = new();
    private readonly Mock<IUserRepository>               _userRepo            = new();
    private readonly Mock<IAuthorNotificationRepository> _notificationRepo    = new();

    private ReadingProgressService CreateSut() => new(
        _readEventRepo.Object,
        _sectionRepo.Object,
        _passageAnchorService.Object,
        _unitOfWork.Object,
        _userRepo.Object,
        _notificationRepo.Object);

    private static Section MakePublishedSection(Guid projectId)
    {
        var s = Section.CreateDocument(projectId, Guid.NewGuid().ToString(),
            "Scene 1", null, 0, "<p>x</p>", "h", "First Draft");
        s.PublishAsPartOfChapter("h");
        return s;
    }

    // ---------------------------------------------------------------------------
    // RecordOpen - new event
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RecordOpenAsync_NoExistingEvent_CreatesReadEvent()
    {
        var sectionId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync((ReadEvent?)null);

        ReadEvent? added = null;
        _readEventRepo.Setup(r => r.AddAsync(It.IsAny<ReadEvent>(), default))
            .Callback<ReadEvent, CancellationToken>((e, _) => added = e);

        await sut.RecordOpenAsync(sectionId, userId);

        Assert.NotNull(added);
        Assert.Equal(1, added!.OpenCount);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // RecordOpen - existing event
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RecordOpenAsync_ExistingEvent_IncrementsOpenCount()
    {
        var sectionId  = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var existing   = ReadEvent.Create(sectionId, userId);
        var sut        = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(existing);

        await sut.RecordOpenAsync(sectionId, userId);

        Assert.Equal(2, existing.OpenCount);
    }

    [Fact]
    public async Task RecordOpenAsync_ExistingEvent_DoesNotAddNewEvent()
    {
        var sectionId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var existing  = ReadEvent.Create(sectionId, userId);
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(existing);

        await sut.RecordOpenAsync(sectionId, userId);

        _readEventRepo.Verify(r => r.AddAsync(It.IsAny<ReadEvent>(), default), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // IsCaughtUp
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task IsCaughtUpAsync_AllSectionsRead_ReturnsTrue()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedSection(projectId);
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section> { section });

        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default))
            .ReturnsAsync(true);

        var result = await sut.IsCaughtUpAsync(userId, projectId);

        Assert.True(result);
    }

    [Fact]
    public async Task IsCaughtUpAsync_UnreadSection_ReturnsFalse()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedSection(projectId);
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section> { section });

        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default))
            .ReturnsAsync(false);

        var result = await sut.IsCaughtUpAsync(userId, projectId);

        Assert.False(result);
    }

    [Fact]
    public async Task IsCaughtUpAsync_NoPublishedSections_ReturnsTrue()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section>());

        var result = await sut.IsCaughtUpAsync(userId, projectId);

        Assert.True(result);
    }

    // ---------------------------------------------------------------------------
    // GetLastReadEventAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetLastReadEventAsync_NoEvents_ReturnsNull()
    {
        var userId    = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<ReadEvent>());

        var result = await sut.GetLastReadEventAsync(userId, projectId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastReadEventAsync_EventsForOtherUser_ReturnsNull()
    {
        var userId      = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var projectId   = Guid.NewGuid();
        var sectionId   = Guid.NewGuid();
        var sut         = CreateSut();

        var ev = ReadEvent.Create(sectionId, otherUserId);
        _readEventRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<ReadEvent> { ev });

        var result = await sut.GetLastReadEventAsync(userId, projectId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastReadEventAsync_SingleEvent_ReturnsThatEvent()
    {
        var userId    = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var sut       = CreateSut();

        var ev = ReadEvent.Create(sectionId, userId);
        _readEventRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<ReadEvent> { ev });

        var result = await sut.GetLastReadEventAsync(userId, projectId);

        Assert.NotNull(result);
        Assert.Equal(sectionId, result!.SectionId);
    }

    [Fact]
    public async Task GetLastReadEventAsync_MultipleEvents_ReturnsMostRecentByLastOpenedAt()
    {
        var userId    = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var section1  = Guid.NewGuid();
        var section2  = Guid.NewGuid();
        var sut       = CreateSut();

        var ev1 = ReadEvent.Create(section1, userId);
        var ev2 = ReadEvent.Create(section2, userId);
        ev2.RecordOpen();

        _readEventRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<ReadEvent> { ev1, ev2 });

        var result = await sut.GetLastReadEventAsync(userId, projectId);

        Assert.NotNull(result);
        Assert.Equal(section2, result!.SectionId);
    }

    // ---------------------------------------------------------------------------
    // UpdateLastReadVersionAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateLastReadVersionAsync_UpdatesVersionNumber_WhenReadEventExists()
    {
        var sectionId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var sut       = CreateSut();

        var readEvent = ReadEvent.Create(sectionId, userId);
        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);

        await sut.UpdateLastReadVersionAsync(sectionId, userId, 3);

        Assert.Equal(3, readEvent.LastReadVersionNumber);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UpdateLastReadVersionAsync_DoesNotThrow_WhenNoReadEventExists()
    {
        var sectionId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync((ReadEvent?)null);

        await sut.UpdateLastReadVersionAsync(sectionId, userId, 3);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task DismissBannerAsync_SetsBannerDismissedAtVersion_WhenReadEventExists()
    {
        var sectionId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var sut       = CreateSut();

        var readEvent = ReadEvent.Create(sectionId, userId);
        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);

        await sut.DismissBannerAsync(sectionId, userId, 4);

        Assert.Equal(4, readEvent.BannerDismissedAtVersion);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task DismissBannerAsync_DoesNotThrow_WhenNoReadEventExists()
    {
        var sectionId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync((ReadEvent?)null);

        await sut.DismissBannerAsync(sectionId, userId, 4);

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task CaptureResumePositionAsync_ExistingReadEvent_CreatesResumeAnchorAndUpdatesReadEvent()
    {
        var sectionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var readEvent = ReadEvent.Create(sectionId, userId);
        var sut = CreateSut();
        var request = CreateCaptureRequest(sectionId);

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);
        _passageAnchorService.Setup(s => s.CreateAsync(
                It.Is<CreatePassageAnchorRequest>(r =>
                    r.SectionId == sectionId &&
                    r.Purpose == PassageAnchorPurpose.Resume &&
                    r.SelectedText == request.SelectedText),
                userId,
                default))
            .ReturnsAsync(new PassageAnchorDto(
                anchorId,
                sectionId,
                request.OriginalSectionVersionId,
                PassageAnchorPurpose.Resume,
                userId,
                DateTime.UtcNow,
                PassageAnchorStatus.Original,
                null,
                new PassageAnchorSnapshotDto(
                    request.SelectedText,
                    request.NormalizedSelectedText,
                    request.SelectedTextHash,
                    request.PrefixContext,
                    request.SuffixContext,
                    request.StartOffset,
                    request.EndOffset,
                    request.CanonicalContentHash,
                    request.HtmlSelectorHint),
                null));

        await sut.CaptureResumePositionAsync(request, userId);

        Assert.Equal(anchorId, readEvent.ResumeAnchorId);
        _readEventRepo.Verify(r => r.AddAsync(It.IsAny<ReadEvent>(), default), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CaptureResumePositionAsync_NoReadEvent_CreatesReadEventAndSetsResumeAnchor()
    {
        var sectionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var sut = CreateSut();
        var request = CreateCaptureRequest(sectionId);

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync((ReadEvent?)null);
        _passageAnchorService.Setup(s => s.CreateAsync(It.IsAny<CreatePassageAnchorRequest>(), userId, default))
            .ReturnsAsync(new PassageAnchorDto(
                anchorId,
                sectionId,
                request.OriginalSectionVersionId,
                PassageAnchorPurpose.Resume,
                userId,
                DateTime.UtcNow,
                PassageAnchorStatus.Original,
                null,
                new PassageAnchorSnapshotDto(
                    request.SelectedText,
                    request.NormalizedSelectedText,
                    request.SelectedTextHash,
                    request.PrefixContext,
                    request.SuffixContext,
                    request.StartOffset,
                    request.EndOffset,
                    request.CanonicalContentHash,
                    request.HtmlSelectorHint),
                null));

        ReadEvent? added = null;
        _readEventRepo.Setup(r => r.AddAsync(It.IsAny<ReadEvent>(), default))
            .Callback<ReadEvent, CancellationToken>((eventItem, _) => added = eventItem)
            .Returns(Task.CompletedTask);

        await sut.CaptureResumePositionAsync(request, userId);

        Assert.NotNull(added);
        Assert.Equal(anchorId, added!.ResumeAnchorId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CaptureResumePositionAsync_InvalidPosition_PropagatesInvariantViolationException()
    {
        var sectionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sut = CreateSut();

        _passageAnchorService.Setup(s => s.CreateAsync(It.IsAny<CreatePassageAnchorRequest>(), userId, default))
            .ThrowsAsync(new InvariantViolationException("I-ANCHOR-SELECTION", "Invalid position."));

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.CaptureResumePositionAsync(CreateCaptureRequest(sectionId), userId));

        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task GetResumeRestoreTargetAsync_OriginalAnchorOnCurrentVersion_ReturnsExactTarget()
    {
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var readEvent = ReadEvent.Create(sectionId, userId);
        readEvent.UpdateResumeAnchor(anchorId);
        var sut = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);
        _passageAnchorService.Setup(s => s.ResolveCurrentMatchAsync(anchorId, userId, default))
            .ReturnsAsync(new PassageAnchorDto(
                anchorId,
                sectionId,
                versionId,
                PassageAnchorPurpose.Resume,
                userId,
                DateTime.UtcNow,
                PassageAnchorStatus.Original,
                null,
                new PassageAnchorSnapshotDto(
                    "Alpha beta",
                    "Alpha beta",
                    "selected-hash",
                    string.Empty,
                    " gamma",
                    0,
                    10,
                    "content-hash",
                    "#scene"),
                null));

        var result = await sut.GetResumeRestoreTargetAsync(sectionId, versionId, userId);

        Assert.NotNull(result);
        Assert.True(result!.HasTarget);
        Assert.Equal(PassageAnchorStatus.Original, result.Status);
        Assert.Equal(0, result.StartOffset);
        Assert.Equal(10, result.EndOffset);
        Assert.Equal(100, result.ConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Exact, result.MatchMethod);
    }

    [Fact]
    public async Task GetResumeRestoreTargetAsync_ContextMatchedAnchor_ReturnsCurrentMatchMetadata()
    {
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var readEvent = ReadEvent.Create(sectionId, userId);
        readEvent.UpdateResumeAnchor(anchorId);
        var sut = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);
        _passageAnchorService.Setup(s => s.ResolveCurrentMatchAsync(anchorId, userId, default))
            .ReturnsAsync(new PassageAnchorDto(
                anchorId,
                sectionId,
                Guid.NewGuid(),
                PassageAnchorPurpose.Resume,
                userId,
                DateTime.UtcNow,
                PassageAnchorStatus.Context,
                DateTime.UtcNow,
                new PassageAnchorSnapshotDto(
                    "Alpha beta",
                    "Alpha beta",
                    "selected-hash",
                    string.Empty,
                    " gamma",
                    0,
                    10,
                    "content-hash",
                    "#scene"),
                new PassageAnchorMatchDto(
                    versionId,
                    12,
                    22,
                    "Alpha beta",
                    84,
                    PassageAnchorMatchMethod.Context,
                    DateTime.UtcNow,
                    null,
                    "Context matched.")));

        var result = await sut.GetResumeRestoreTargetAsync(sectionId, versionId, userId);

        Assert.NotNull(result);
        Assert.True(result!.HasTarget);
        Assert.Equal(PassageAnchorStatus.Context, result.Status);
        Assert.Equal(12, result.StartOffset);
        Assert.Equal(22, result.EndOffset);
        Assert.Equal(84, result.ConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Context, result.MatchMethod);
    }

    [Fact]
    public async Task GetResumeRestoreTargetAsync_ExactCrossVersionMatch_ReturnsExactCrossVersionTarget()
    {
        var sectionId = Guid.NewGuid();
        var originalVersionId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var readEvent = ReadEvent.Create(sectionId, userId);
        readEvent.UpdateResumeAnchor(anchorId);
        var sut = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);
        _passageAnchorService.Setup(s => s.ResolveCurrentMatchAsync(anchorId, userId, default))
            .ReturnsAsync(new PassageAnchorDto(
                anchorId,
                sectionId,
                originalVersionId,
                PassageAnchorPurpose.Resume,
                userId,
                DateTime.UtcNow,
                PassageAnchorStatus.Exact,
                DateTime.UtcNow,
                new PassageAnchorSnapshotDto(
                    "Alpha beta",
                    "Alpha beta",
                    "selected-hash",
                    string.Empty,
                    " gamma",
                    0,
                    10,
                    "content-hash",
                    "#scene"),
                new PassageAnchorMatchDto(
                    currentVersionId,
                    0,
                    10,
                    "Alpha beta",
                    100,
                    PassageAnchorMatchMethod.Exact,
                    DateTime.UtcNow,
                    null,
                    null)));

        var result = await sut.GetResumeRestoreTargetAsync(sectionId, currentVersionId, userId);

        Assert.NotNull(result);
        Assert.True(result!.HasTarget);
        Assert.Equal(PassageAnchorStatus.Exact, result.Status);
        Assert.Equal(0, result.StartOffset);
        Assert.Equal(10, result.EndOffset);
        Assert.Equal(100, result.ConfidenceScore);
        Assert.Equal(PassageAnchorMatchMethod.Exact, result.MatchMethod);
    }

    [Fact]
    public async Task GetResumeRestoreTargetAsync_OrphanedAnchor_ReturnsSafeFallback()
    {
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var readEvent = ReadEvent.Create(sectionId, userId);
        readEvent.UpdateResumeAnchor(anchorId);
        var sut = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);
        _passageAnchorService.Setup(s => s.ResolveCurrentMatchAsync(anchorId, userId, default))
            .ReturnsAsync(new PassageAnchorDto(
                anchorId,
                sectionId,
                versionId,
                PassageAnchorPurpose.Resume,
                userId,
                DateTime.UtcNow,
                PassageAnchorStatus.Orphaned,
                DateTime.UtcNow,
                new PassageAnchorSnapshotDto(
                    "Alpha beta",
                    "Alpha beta",
                    "selected-hash",
                    string.Empty,
                    " gamma",
                    0,
                    10,
                    "content-hash",
                    "#scene"),
                null));

        var result = await sut.GetResumeRestoreTargetAsync(sectionId, versionId, userId);

        Assert.NotNull(result);
        Assert.False(result!.HasTarget);
        Assert.Equal(PassageAnchorStatus.Orphaned, result.Status);
        Assert.Null(result.StartOffset);
        Assert.Null(result.EndOffset);
        Assert.Null(result.ConfidenceScore);
        Assert.Null(result.MatchMethod);
    }

    [Fact]
    public async Task GetResumeRestoreTargetAsync_InaccessibleAnchor_PropagatesUnauthorisedOperationException()
    {
        var sectionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var readEvent = ReadEvent.Create(sectionId, userId);
        readEvent.UpdateResumeAnchor(anchorId);
        var sut = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(sectionId, userId, default))
            .ReturnsAsync(readEvent);
        _passageAnchorService.Setup(s => s.ResolveCurrentMatchAsync(anchorId, userId, default))
            .ThrowsAsync(new UnauthorisedOperationException("Forbidden"));

        await Assert.ThrowsAsync<UnauthorisedOperationException>(
            () => sut.GetResumeRestoreTargetAsync(sectionId, versionId, userId));
    }

    private static CaptureResumePositionRequest CreateCaptureRequest(Guid sectionId)
    {
        return new CaptureResumePositionRequest(
            sectionId,
            Guid.NewGuid(),
            "Alpha beta",
            "Alpha beta",
            "selected-hash",
            string.Empty,
            " gamma",
            0,
            10,
            "content-hash",
            "#scene");
    }

    // ---------------------------------------------------------------------------
    // RecordOpen — reader notifications
    // ---------------------------------------------------------------------------

    private static Section MakePublishedDocument(Guid projectId, string title = "Scene 1")
    {
        var s = Section.CreateDocument(projectId, Guid.NewGuid().ToString(),
            title, null, 0, "<p>x</p>", "h", "First Draft");
        s.PublishAsPartOfChapter("h");
        return s;
    }

    private static ReadEvent MakeOldReadEvent(Guid sectionId, Guid userId, int daysAgo)
    {
        var ev = ReadEvent.Create(sectionId, userId);
        typeof(ReadEvent)
            .GetProperty(nameof(ReadEvent.LastOpenedAt))!
            .SetValue(ev, DateTime.UtcNow.AddDays(-daysAgo));
        return ev;
    }

    private void SetupNotificationDeps(User author, User reader, Guid userId, Section section)
    {
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _readEventRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(new List<ReadEvent>());
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(section.ProjectId, default))
            .ReturnsAsync(new List<Section> { section });
        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default)).ReturnsAsync(false);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_DocumentSection_WritesReaderReadNewSceneNotification()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedDocument(projectId);
        var author    = User.Create("a@test.com", "Author Name", Role.Author);
        var reader    = User.Create("r@test.com", "Reader Name", Role.BetaReader);
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        SetupNotificationDeps(author, reader, userId, section);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.EventType == NotificationEventType.ReaderReadNewScene &&
                n.AuthorId  == author.Id &&
                n.Title.Contains("Reader Name") &&
                n.Title.Contains(section.Title)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_FolderSection_DoesNotWriteNotification()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var folder    = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(folder.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        _readEventRepo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(new List<ReadEvent>());
        _sectionRepo.Setup(r => r.GetByIdAsync(folder.Id, default)).ReturnsAsync(folder);

        await sut.RecordOpenAsync(folder.Id, userId);

        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<AuthorNotification>(), default), Times.Never);
    }

    [Fact]
    public async Task RecordOpenAsync_RepeatOpen_DoesNotWriteReaderReadNewSceneNotification()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedDocument(projectId);
        var existing  = ReadEvent.Create(section.Id, userId);
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync(existing);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(r => r.AddAsync(It.IsAny<AuthorNotification>(), default), Times.Never);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_WithPreviousEventsOlderThan7Days_WritesReaderReturnedNotification()
    {
        var projectId  = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var section    = MakePublishedDocument(projectId);
        var oldSection = Guid.NewGuid();
        var author     = User.Create("a@test.com", "Author Name", Role.Author);
        var reader     = User.Create("r@test.com", "Reader Name", Role.BetaReader);
        var sut        = CreateSut();

        var oldEvent = MakeOldReadEvent(oldSection, userId, daysAgo: 10);

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _readEventRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(new List<ReadEvent> { oldEvent });
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(section.ProjectId, default))
            .ReturnsAsync(new List<Section> { section });
        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default)).ReturnsAsync(false);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.EventType == NotificationEventType.ReaderReturned &&
                n.AuthorId  == author.Id &&
                n.Title.Contains("Reader Name")),
                default),
            Times.Once);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_WithPreviousEventsWithin7Days_DoesNotWriteReaderReturned()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedDocument(projectId);
        var author    = User.Create("a@test.com", "Author Name", Role.Author);
        var reader    = User.Create("r@test.com", "Reader Name", Role.BetaReader);
        var sut       = CreateSut();

        var recentEvent = MakeOldReadEvent(Guid.NewGuid(), userId, daysAgo: 3);

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _readEventRepo.Setup(r => r.GetByUserIdAsync(userId, default))
            .ReturnsAsync(new List<ReadEvent> { recentEvent });
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(section.ProjectId, default))
            .ReturnsAsync(new List<Section> { section });
        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default)).ReturnsAsync(false);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.EventType == NotificationEventType.ReaderReturned),
                default),
            Times.Never);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_NoPreviousEvents_DoesNotWriteReaderReturned()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedDocument(projectId);
        var author    = User.Create("a@test.com", "Author Name", Role.Author);
        var reader    = User.Create("r@test.com", "Reader Name", Role.BetaReader);
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        SetupNotificationDeps(author, reader, userId, section);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.EventType == NotificationEventType.ReaderReturned),
                default),
            Times.Never);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_ReaderNowCaughtUp_WritesReaderFinishedManuscriptNotification()
    {
        var projectId = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var section   = MakePublishedDocument(projectId);
        var author    = User.Create("a@test.com", "Author Name", Role.Author);
        var reader    = User.Create("r@test.com", "Reader Name", Role.BetaReader);
        var sut       = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _readEventRepo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(new List<ReadEvent>());
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(section.ProjectId, default))
            .ReturnsAsync(new List<Section> { section });
        // After adding the new ReadEvent the reader has read this section
        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default)).ReturnsAsync(true);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.EventType == NotificationEventType.ReaderFinishedManuscript &&
                n.AuthorId  == author.Id &&
                n.Title.Contains("Reader Name")),
                default),
            Times.Once);
    }

    [Fact]
    public async Task RecordOpenAsync_FirstOpen_ReaderNotCaughtUp_DoesNotWriteReaderFinishedManuscript()
    {
        var projectId   = Guid.NewGuid();
        var userId      = Guid.NewGuid();
        var section     = MakePublishedDocument(projectId);
        var unread      = MakePublishedDocument(projectId, "Scene 2");
        var author      = User.Create("a@test.com", "Author Name", Role.Author);
        var reader      = User.Create("r@test.com", "Reader Name", Role.BetaReader);
        var sut         = CreateSut();

        _readEventRepo.Setup(r => r.GetAsync(section.Id, userId, default)).ReturnsAsync((ReadEvent?)null);
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(reader);
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _readEventRepo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(new List<ReadEvent>());
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(section.ProjectId, default))
            .ReturnsAsync(new List<Section> { section, unread });
        _readEventRepo.Setup(r => r.HasReadAsync(section.Id, userId, default)).ReturnsAsync(true);
        _readEventRepo.Setup(r => r.HasReadAsync(unread.Id, userId, default)).ReturnsAsync(false);

        await sut.RecordOpenAsync(section.Id, userId);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.EventType == NotificationEventType.ReaderFinishedManuscript),
                default),
            Times.Never);
    }
}
