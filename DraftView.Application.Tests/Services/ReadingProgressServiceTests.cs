using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class ReadingProgressServiceTests
{
    private readonly Mock<IReadEventRepository> _readEventRepo = new();
    private readonly Mock<ISectionRepository>   _sectionRepo   = new();
    private readonly Mock<IUnitOfWork>          _unitOfWork    = new();

    private ReadingProgressService CreateSut() => new(
        _readEventRepo.Object,
        _sectionRepo.Object,
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
}

