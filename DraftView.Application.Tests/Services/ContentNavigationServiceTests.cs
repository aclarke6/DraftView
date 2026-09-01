using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;

namespace DraftView.Application.Tests.Services;

public class ContentNavigationServiceTests
{
    private readonly Mock<ISectionRepository> _sectionRepo = new();

    private ContentNavigationService CreateSut() => new(_sectionRepo.Object);

    [Fact]
    public async Task BuildPublishingChapterDataAsync_NoSections_ReturnsEmpty()
    {
        var projectId = Guid.NewGuid();
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.ScrivenerDropbox);

        Assert.Empty(result);
    }

    [Fact]
    public async Task BuildPublishingChapterDataAsync_UnpublishedChapter_ReturnsEmpty()
    {
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter]);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.ScrivenerDropbox);

        Assert.Empty(result);
    }

    [Fact]
    public async Task BuildPublishingChapterDataAsync_PublishedChapterWithDocument_ReturnsOneChapter()
    {
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(projectId, Guid.NewGuid().ToString(), "Scene 1",
            chapter.Id, 0, "<p>text</p>", "hash", null);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.ScrivenerDropbox);

        var chapterData = Assert.Single(result);
        Assert.Equal(chapter.Id, chapterData.Chapter.Id);
        Assert.Single(chapterData.Documents);
        Assert.Equal(document.Id, chapterData.Documents[0].Document.Id);
    }

    [Fact]
    public async Task BuildPublishingChapterDataAsync_SoftDeletedDocument_IsExcluded()
    {
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(projectId, Guid.NewGuid().ToString(), "Scene 1",
            chapter.Id, 0, "<p>text</p>", "hash", null);
        document.SoftDelete();

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.ScrivenerDropbox);

        var chapterData = Assert.Single(result);
        Assert.Empty(chapterData.Documents);
    }

    [Fact]
    public async Task BuildPublishingChapterDataAsync_ManualProject_ShowsDocumentControls()
    {
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(projectId, Guid.NewGuid().ToString(), "Scene 1",
            chapter.Id, 0, "<p>text</p>", "hash", null);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.Manual);

        Assert.True(result[0].ShowDocumentControls);
    }
}
