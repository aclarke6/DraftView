using Moq;
using DraftReader.Application.Services;
using DraftReader.Domain.Entities;
using DraftReader.Domain.Enumerations;
using DraftReader.Domain.Exceptions;
using DraftReader.Domain.Interfaces.Repositories;

namespace DraftReader.Application.Tests.Services;

public class PublicationServiceTests
{
    private readonly Mock<ISectionRepository> _sectionRepo = new();
    private readonly Mock<IUnitOfWork>        _unitOfWork  = new();

    private PublicationService CreateSut() => new(
        _sectionRepo.Object,
        _unitOfWork.Object);

    private static Section MakeChapter(Guid projectId) =>
        Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);

    private static Section MakeScene(Guid projectId, Guid chapterId, string status) =>
        Section.CreateDocument(projectId, Guid.NewGuid().ToString(),
            "Scene 1", chapterId, 0, "<p>x</p>", "hash", status);

    // ---------------------------------------------------------------------------
    // PublishChapter
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PublishChapterAsync_AllScenesReady_PublishesAll()
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var scene     = MakeScene(projectId, chapter.Id, "First Draft");
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { scene });

        await sut.PublishChapterAsync(chapter.Id, Guid.NewGuid());

        Assert.True(chapter.IsPublished);
        Assert.True(scene.IsPublished);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task PublishChapterAsync_SceneNotReady_ThrowsInvariantViolation()
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var scene     = MakeScene(projectId, chapter.Id, "To Do");
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { scene });

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.PublishChapterAsync(chapter.Id, Guid.NewGuid()));

        Assert.Equal("I-CHAPTER-PUBLISH", ex.InvariantCode);
    }

    [Fact]
    public async Task PublishChapterAsync_ChapterNotFound_ThrowsEntityNotFoundException()
    {
        var sut = CreateSut();
        var missingId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(missingId, default))
            .ReturnsAsync((Section?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.PublishChapterAsync(missingId, Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------------
    // UnpublishChapter
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UnpublishChapterAsync_UnpublishesChapterAndScenes()
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var scene     = MakeScene(projectId, chapter.Id, "First Draft");
        chapter.MarkAsPublishedContainer();
        scene.PublishAsPartOfChapter("hash");
        var sut = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { scene });

        await sut.UnpublishChapterAsync(chapter.Id, Guid.NewGuid());

        Assert.False(chapter.IsPublished);
        Assert.False(scene.IsPublished);
    }

    // ---------------------------------------------------------------------------
    // CanPublish
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CanPublishAsync_AllScenesReady_ReturnsTrue()
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var scene     = MakeScene(projectId, chapter.Id, "First Draft");
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { scene });

        var result = await sut.CanPublishAsync(chapter.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task CanPublishAsync_SceneNotReady_ReturnsFalse()
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var scene     = MakeScene(projectId, chapter.Id, "To Do");
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { scene });

        var result = await sut.CanPublishAsync(chapter.Id);

        Assert.False(result);
    }
}


