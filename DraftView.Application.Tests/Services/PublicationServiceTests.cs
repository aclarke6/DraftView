using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class PublicationServiceTests
{
    private readonly Mock<ISectionRepository> _sectionRepo = new();
    private readonly Mock<IUnitOfWork>        _unitOfWork  = new();

    private PublicationService CreateSut() => new(
        _sectionRepo.Object,
        _unitOfWork.Object);

    private static Section MakeChapter(Guid projectId) =>
        Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);

    private static Section MakeScene(Guid projectId, Guid chapterId, string? status) =>
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("In Progress")]
    public async Task PublishChapterAsync_SceneWithNoStatusOrInProgress_ThrowsInvariantViolation(string? status)
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var scene     = MakeScene(projectId, chapter.Id, status);
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { scene });

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.PublishChapterAsync(chapter.Id, Guid.NewGuid()));

        Assert.Equal("I-CHAPTER-PUBLISH", ex.InvariantCode);
        Assert.False(scene.IsPublished);
        Assert.False(chapter.IsPublished);
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
    // GetPublishedChapters
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetPublishedChaptersAsync_ReturnsChaptersInDepthFirstTreeOrder()
    {
        var projectId = Guid.NewGuid();

        // Structure: Part A (sort 0) -> Chapter 1 (sort 0), Chapter 2 (sort 1)
        //            Part B (sort 1) -> Chapter 3 (sort 0)
        var partA    = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Part A",    null,      0);
        var partB    = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Part B",    null,      1);
        var chap1    = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", partA.Id,  0);
        var chap2    = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 2", partA.Id,  1);
        var chap3    = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 3", partB.Id,  0);

        chap1.MarkAsPublishedContainer();
        chap2.MarkAsPublishedContainer();
        chap3.MarkAsPublishedContainer();

        var sut = CreateSut();
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section> { partB, chap3, partA, chap2, chap1 }); // deliberately shuffled

        var result = await sut.GetPublishedChaptersAsync(projectId);

        Assert.Equal(3, result.Count);
        Assert.Equal("Chapter 1", result[0].Title);
        Assert.Equal("Chapter 2", result[1].Title);
        Assert.Equal("Chapter 3", result[2].Title);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("In Progress")]
    public async Task CanPublishAsync_SceneWithNoStatusOrInProgress_ReturnsFalse(string? status)
    {
        var projectId = Guid.NewGuid();
        var chapter   = MakeChapter(projectId);
        var ready     = MakeScene(projectId, chapter.Id, "First Draft");
        var notReady  = MakeScene(projectId, chapter.Id, status);
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { ready, notReady });

        var result = await sut.CanPublishAsync(chapter.Id);

        Assert.False(result);
    }
}
