using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for SectionManagementService.GetSectionsSummaryAsync.
/// Covers: depth-first ordering, publishability flagging, change classification
/// for published chapters with edited documents, and null return for unknown projects.
/// Excludes: EF Core persistence (unit tests only), UI rendering.
/// </summary>
public class SectionManagementServiceTests
{
    private readonly Mock<IProjectRepository>              _projectRepo               = new();
    private readonly Mock<ISectionRepository>               _sectionRepo               = new();
    private readonly Mock<ISectionVersionRepository>         _sectionVersionRepo        = new();
    private readonly Mock<IPublicationService>                _publicationService        = new();
    private readonly Mock<IHtmlDiffService>                    _htmlDiffService           = new();
    private readonly Mock<IChangeClassificationService>          _changeClassificationService = new();

    private SectionManagementService CreateSut() => new(
        _projectRepo.Object,
        _sectionRepo.Object,
        _sectionVersionRepo.Object,
        _publicationService.Object,
        _htmlDiffService.Object,
        _changeClassificationService.Object);

    [Fact]
    public async Task GetSectionsSummaryAsync_ProjectNotFound_ReturnsNull()
    {
        var projectId = Guid.NewGuid();
        _projectRepo.Setup(r => r.GetByIdAsync(projectId, default)).ReturnsAsync((Project?)null);

        var result = await CreateSut().GetSectionsSummaryAsync(projectId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSectionsSummaryAsync_SortsSectionsDepthFirst()
    {
        var project   = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var chapter2  = Section.CreateFolder(project.Id, "c2", "Chapter 2", null, 1);
        var chapter1  = Section.CreateFolder(project.Id, "c1", "Chapter 1", null, 0);
        var scene1    = Section.CreateDocument(project.Id, "s1", "Scene 1", chapter1.Id, 0, "<p>a</p>", "h1", "First Draft");

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { chapter2, chapter1, scene1 });
        _publicationService.Setup(p => p.CanPublishAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);

        var result = await CreateSut().GetSectionsSummaryAsync(project.Id);

        Assert.NotNull(result);
        Assert.Equal(
            new[] { chapter1.Id, scene1.Id, chapter2.Id },
            result!.SortedSections.Select(r => r.Section.Id));
        Assert.Equal(0, result.SortedSections[0].Depth);
        Assert.Equal(1, result.SortedSections[1].Depth);
    }

    [Fact]
    public async Task GetSectionsSummaryAsync_MarksPublishableFolders()
    {
        var project = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var chapter = Section.CreateFolder(project.Id, "c1", "Chapter 1", null, 0);

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { chapter });
        _publicationService.Setup(p => p.CanPublishAsync(chapter.Id, default)).ReturnsAsync(true);

        var result = await CreateSut().GetSectionsSummaryAsync(project.Id);

        Assert.Contains(chapter.Id, result!.Publishable);
    }

    [Fact]
    public async Task GetSectionsSummaryAsync_ClassifiesChangesForPublishedChapterWithEditedDocuments()
    {
        var project = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var chapter = Section.CreateFolder(project.Id, "c1", "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();

        var document = Section.CreateDocument(project.Id, "s1", "Scene 1", chapter.Id, 0, "<p>old</p>", "h1", "First Draft");
        var version = SectionVersion.Create(document, Guid.NewGuid(), 1, 1, 0);
        document.UpdateContent("<p>new</p>", "h2");
        document.MarkContentChanged();

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { chapter, document });
        _publicationService.Setup(p => p.CanPublishAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);
        _sectionVersionRepo.Setup(r => r.GetLatestAsync(document.Id, default)).ReturnsAsync(version);
        _htmlDiffService.Setup(d => d.Compute(version.HtmlContent, document.HtmlContent))
            .Returns(new List<ParagraphDiffResult>());
        _changeClassificationService
            .Setup(c => c.Classify(It.IsAny<IReadOnlyList<ParagraphDiffResult>>()))
            .Returns(ChangeClassification.Rewrite);

        var result = await CreateSut().GetSectionsSummaryAsync(project.Id);

        Assert.Contains(chapter.Id, result!.ChapterHasChanges);
        Assert.Equal(ChangeClassification.Rewrite, result.ClassificationMap[chapter.Id]);
    }

    [Fact]
    public async Task GetSectionsSummaryAsync_PublishedChapterWithoutContentChanges_NotFlagged()
    {
        var project = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var chapter = Section.CreateFolder(project.Id, "c1", "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();

        var document = Section.CreateDocument(project.Id, "s1", "Scene 1", chapter.Id, 0, "<p>content</p>", "h1", "First Draft");

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { chapter, document });
        _publicationService.Setup(p => p.CanPublishAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);

        var result = await CreateSut().GetSectionsSummaryAsync(project.Id);

        Assert.DoesNotContain(chapter.Id, result!.ChapterHasChanges);
        Assert.False(result.ClassificationMap.ContainsKey(chapter.Id));
    }
}
