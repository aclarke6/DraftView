using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ContentNavigationService.BuildPublishingChapterDataAsync.
/// Covers: empty project, published leaf chapter detection, document inclusion,
/// version history at retention limit, classification tolerance, soft-deleted exclusion.
/// Excludes: EF Core persistence, controller mapping, subscription-tier enforcement.
/// </summary>
public class ContentNavigationServiceTests
{
    private readonly Mock<ISectionRepository>          _sectionRepo          = new();
    private readonly Mock<ISectionVersionRepository>   _sectionVersionRepo   = new();
    private readonly Mock<IHtmlDiffService>            _htmlDiffService      = new();
    private readonly Mock<IChangeClassificationService> _classificationService = new();
    private readonly Mock<IConfiguration>              _configuration        = new();

    private ContentNavigationService CreateSut() => new(
        _sectionRepo.Object,
        _sectionVersionRepo.Object,
        _htmlDiffService.Object,
        _classificationService.Object,
        _configuration.Object);

    private void SetupNoVersions(Guid documentId)
    {
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionVersion?)null);
    }

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
        var authorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(projectId, Guid.NewGuid().ToString(), "Scene 1",
            chapter.Id, 0, "<p>text</p>", "hash", null);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);
        SetupNoVersions(document.Id);

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
    public async Task BuildPublishingChapterDataAsync_AtRetentionLimit_ShowsVersionHistory()
    {
        var authorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(projectId, Guid.NewGuid().ToString(), "Scene 1",
            chapter.Id, 0, "<p>text</p>", "hash", null);

        var v1 = SectionVersion.Create(document, authorId, 1, 1, 0);
        var v2 = SectionVersion.Create(document, authorId, 2, 1, 1);
        var v3 = SectionVersion.Create(document, authorId, 3, 1, 2);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Free");
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(v3);
        _sectionVersionRepo.Setup(r => r.GetAllBySectionIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([v1, v2, v3]);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.ScrivenerDropbox);

        var docData = Assert.Single(result[0].Documents);
        Assert.True(docData.ShowVersionHistory);
        Assert.Equal(3, docData.VersionHistory.Count);
        Assert.False(docData.VersionHistory.First(v => v.VersionNumber == 3).CanDelete);
        Assert.True(docData.VersionHistory.First(v => v.VersionNumber == 2).CanDelete);
    }

    [Fact]
    public async Task BuildPublishingChapterDataAsync_ClassificationException_DoesNotPropagate()
    {
        var authorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var chapter = Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(projectId, Guid.NewGuid().ToString(), "Scene 1",
            chapter.Id, 0, "<p>new</p>", "hash", null);

        var latestVersion = SectionVersion.Create(document, authorId, 1, 1, 0);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);
        _sectionVersionRepo.Setup(r => r.GetAllBySectionIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([latestVersion]);
        _htmlDiffService.Setup(s => s.Compute(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("diff failure"));

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.ScrivenerDropbox);

        var docData = Assert.Single(result[0].Documents);
        Assert.Null(docData.Classification);
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
        SetupNoVersions(document.Id);

        var result = await CreateSut().BuildPublishingChapterDataAsync(projectId, ProjectType.Manual);

        Assert.True(result[0].ShowDocumentControls);
    }
}
