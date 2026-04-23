using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ReadingProgressService orchestration.
/// Covers: read-event creation, progress queries, version tracking, banner dismissal, and resume-anchor capture.
/// Excludes: controller binding, client-side capture JavaScript, and anchor retrieval.
/// </summary>
public class ReadingProgressServiceTests
{
    private readonly Mock<IReadEventRepository> _readEventRepo = new();
    private readonly Mock<ISectionRepository>   _sectionRepo   = new();
    private readonly Mock<IPassageAnchorService> _passageAnchorService = new();
    private readonly Mock<IUnitOfWork>          _unitOfWork    = new();

    private ReadingProgressService CreateSut() => new(
        _readEventRepo.Object,
        _sectionRepo.Object,
        _passageAnchorService.Object,
        _unitOfWork.Object);

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
        _passageAnchorService.Setup(s => s.GetByIdAsync(anchorId, userId, default))
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
        _passageAnchorService.Setup(s => s.GetByIdAsync(anchorId, userId, default))
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
        _passageAnchorService.Setup(s => s.GetByIdAsync(anchorId, userId, default))
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
        _passageAnchorService.Setup(s => s.GetByIdAsync(anchorId, userId, default))
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
}
