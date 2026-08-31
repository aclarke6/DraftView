using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ReaderDiffService.
/// Covers: GetDiffAsync preference gating, MarkAsReadAsync, MarkAsUnreadAsync.
/// Excludes: diff computation logic (SectionDiffServiceTests), classification (ChangeClassificationServiceTests).
/// </summary>
public class ReaderDiffServiceTests
{
    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.NewGuid();

    private readonly Mock<IReadEventRepository>       _readEventRepo        = new();
    private readonly Mock<IUserPreferencesRepository> _userPreferencesRepo  = new();
    private readonly Mock<ISectionVersionRepository>  _sectionVersionRepo   = new();
    private readonly Mock<ISectionDiffService>        _sectionDiffService   = new();
    private readonly Mock<IUnitOfWork>                _unitOfWork           = new();

    private ReaderDiffService CreateSut() => new(
        _readEventRepo.Object,
        _userPreferencesRepo.Object,
        _sectionVersionRepo.Object,
        _sectionDiffService.Object,
        _unitOfWork.Object);

    // ---------------------------------------------------------------------------
    // GetDiffAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetDiffAsync_WhenNoPreferences_ReturnsNull()
    {
        _userPreferencesRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferences?)null);

        var result = await CreateSut().GetDiffAsync(SectionId, UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDiffAsync_WhenShowDiffOnRevisitIsFalse_ReturnsNull()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);
        // ShowDiffOnRevisit defaults to false

        _userPreferencesRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var result = await CreateSut().GetDiffAsync(SectionId, UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDiffAsync_WhenShowDiffOnRevisitIsTrue_CallsDiffServiceWithPreferences()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);
        prefs.UpdateDiffPreferences(showDiffOnRevisit: true, ReadingStyle.StoryReader, diffCooldownHours: 24);

        var readEvent = ReadEvent.Create(SectionId, UserId);
        readEvent.MarkAsRead(3);

        _userPreferencesRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readEvent);
        _sectionDiffService.Setup(s => s.GetDiffForReaderAsync(
                SectionId,
                readEvent.LastReadVersionNumber,
                readEvent.LastMarkedReadAt,
                24,
                ReadingStyle.StoryReader,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionDiffResult?)null);

        await CreateSut().GetDiffAsync(SectionId, UserId);

        _sectionDiffService.Verify(s => s.GetDiffForReaderAsync(
            SectionId,
            readEvent.LastReadVersionNumber,
            readEvent.LastMarkedReadAt,
            24,
            ReadingStyle.StoryReader,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDiffAsync_WhenNoReadEvent_PassesNullVersionAndTimestamp()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);
        prefs.UpdateDiffPreferences(showDiffOnRevisit: true, ReadingStyle.BetaReader, diffCooldownHours: 1);

        _userPreferencesRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadEvent?)null);
        _sectionDiffService.Setup(s => s.GetDiffForReaderAsync(
                SectionId, null, null, 1, ReadingStyle.BetaReader, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionDiffResult?)null);

        await CreateSut().GetDiffAsync(SectionId, UserId);

        _sectionDiffService.Verify(s => s.GetDiffForReaderAsync(
            SectionId, null, null, 1, ReadingStyle.BetaReader,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDiffAsync_ReturnsResultFromDiffService()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);
        prefs.UpdateDiffPreferences(showDiffOnRevisit: true, ReadingStyle.BetaReader, diffCooldownHours: 1);

        var expectedResult = new SectionDiffResult
        {
            FromVersionNumber    = 1,
            CurrentVersionNumber = 2,
            HasChanges           = true,
            Paragraphs           = Array.Empty<ParagraphDiffResult>(),
            Classification       = ChangeClassification.Polish
        };

        _userPreferencesRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadEvent?)null);
        _sectionDiffService.Setup(s => s.GetDiffForReaderAsync(
                It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<DateTimeOffset?>(),
                It.IsAny<int>(), It.IsAny<ReadingStyle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await CreateSut().GetDiffAsync(SectionId, UserId);

        Assert.Same(expectedResult, result);
    }

    // ---------------------------------------------------------------------------
    // MarkAsReadAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkAsReadAsync_WhenReadEventAndVersionExist_CallsMarkAsReadAndSaves()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);
        var section   = CreateSection();
        var version   = CreateVersion(section, 5);

        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        await CreateSut().MarkAsReadAsync(SectionId, UserId);

        Assert.Equal(5, readEvent.LastReadVersionNumber);
        Assert.NotNull(readEvent.LastMarkedReadAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNoReadEvent_DoesNotSave()
    {
        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadEvent?)null);

        await CreateSut().MarkAsReadAsync(SectionId, UserId);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNoVersion_DoesNotSave()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);

        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readEvent);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(SectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionVersion?)null);

        await CreateSut().MarkAsReadAsync(SectionId, UserId);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // MarkAsUnreadAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task MarkAsUnreadAsync_WhenReadEventExists_RestoresPreviousVersionAndSaves()
    {
        var readEvent = ReadEvent.Create(SectionId, UserId);
        readEvent.MarkAsRead(3);
        readEvent.MarkAsRead(5);

        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readEvent);

        await CreateSut().MarkAsUnreadAsync(SectionId, UserId);

        Assert.Equal(3, readEvent.LastReadVersionNumber);
        Assert.Null(readEvent.LastMarkedReadAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsUnreadAsync_WhenNoReadEvent_DoesNotSave()
    {
        _readEventRepo.Setup(r => r.GetAsync(SectionId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadEvent?)null);

        await CreateSut().MarkAsUnreadAsync(SectionId, UserId);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Section CreateSection()
        => Section.CreateDocumentForUpload(Guid.NewGuid(), "Test", null, 1);

    private static SectionVersion CreateVersion(Section section, int versionNumber)
    {
        section.UpdateContent("<p>Content</p>", "hash-" + versionNumber);
        return SectionVersion.Create(section, Guid.NewGuid(), versionNumber, 1, 0);
    }
}
