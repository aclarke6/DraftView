using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for SectionManagementService.GetSectionsSummaryAsync and GetSectionDetailAsync.
/// Covers: depth-first ordering, publishability flagging, change classification
/// for published chapters with edited documents, null return for unknown projects,
/// section detail aggregation.
/// Excludes: EF Core persistence (unit tests only), UI rendering.
/// </summary>
public class SectionManagementServiceTests
{
    private readonly Mock<IProjectRepository>            _projectRepo                 = new();
    private readonly Mock<ISectionRepository>             _sectionRepo                 = new();
    private readonly Mock<ISectionVersionRepository>       _sectionVersionRepo          = new();
    private readonly Mock<IPublicationService>              _publicationService          = new();
    private readonly Mock<IHtmlDiffService>                  _htmlDiffService             = new();
    private readonly Mock<IChangeClassificationService>        _changeClassificationService = new();
    private readonly Mock<ICommentService>                      _commentService              = new();
    private readonly Mock<IUserRepository>                       _userRepository              = new();
    private readonly Mock<IReadEventRepository>                   _readEventRepository         = new();

    private SectionManagementService CreateSut() => new(
        _projectRepo.Object,
        _sectionRepo.Object,
        _sectionVersionRepo.Object,
        _publicationService.Object,
        _htmlDiffService.Object,
        _changeClassificationService.Object,
        _commentService.Object,
        _userRepository.Object,
        _readEventRepository.Object);

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

    // -----------------------------------------------------------------------
    // GetSectionDetailAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSectionDetailAsync_SectionNotFound_ReturnsNull()
    {
        var sectionId = Guid.NewGuid();
        _sectionRepo.Setup(r => r.GetByIdAsync(sectionId, default)).ReturnsAsync((Section?)null);

        var result = await CreateSut().GetSectionDetailAsync(sectionId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSectionDetailAsync_SceneWithParent_ResolvesChapterTitle()
    {
        var project  = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var chapter  = Section.CreateFolder(project.Id, "c1", "Chapter One", null, 0);
        var scene    = Section.CreateDocument(project.Id, "s1", "Scene 1", chapter.Id, 0, "<p>a</p>", "h1", null);
        var authorId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, default)).ReturnsAsync(scene);
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _commentService.Setup(s => s.GetThreadsForSectionAsync(scene.Id, authorId, default)).ReturnsAsync([]);
        _readEventRepository.Setup(r => r.GetBySectionIdAsync(scene.Id, default)).ReturnsAsync([]);

        var result = await CreateSut().GetSectionDetailAsync(scene.Id, authorId);

        Assert.NotNull(result);
        Assert.Equal("Chapter One", result!.ChapterTitle);
        Assert.Same(scene, result.Section);
    }

    [Fact]
    public async Task GetSectionDetailAsync_ReturnsReadCount()
    {
        var project  = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var scene    = Section.CreateDocument(project.Id, "s1", "Scene 1", null, 0, "<p>a</p>", "h1", null);
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid();
        var readEvent1 = ReadEvent.Create(scene.Id, readerId);
        var readEvent2 = ReadEvent.Create(scene.Id, Guid.NewGuid());

        _sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, default)).ReturnsAsync(scene);
        _commentService.Setup(s => s.GetThreadsForSectionAsync(scene.Id, authorId, default)).ReturnsAsync([]);
        _readEventRepository.Setup(r => r.GetBySectionIdAsync(scene.Id, default))
            .ReturnsAsync([readEvent1, readEvent2]);

        var result = await CreateSut().GetSectionDetailAsync(scene.Id, authorId);

        Assert.NotNull(result);
        Assert.Equal(2, result!.ReadCount);
    }

    [Fact]
    public async Task GetSectionDetailAsync_BuildsCommentAuthorNameMap()
    {
        var project    = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var scene      = Section.CreateDocument(project.Id, "s1", "Scene 1", null, 0, "<p>a</p>", "h1", null);
        var authorId   = Guid.NewGuid();
        var readerId   = Guid.NewGuid();
        var readerUser = User.Create("r@example.test", "Alice", Role.BetaReader);
        var comment    = Comment.CreateRoot(scene.Id, readerId, "Great scene!", Domain.Enumerations.Visibility.Public);

        _sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, default)).ReturnsAsync(scene);
        _commentService.Setup(s => s.GetThreadsForSectionAsync(scene.Id, authorId, default))
            .ReturnsAsync([comment]);
        _readEventRepository.Setup(r => r.GetBySectionIdAsync(scene.Id, default)).ReturnsAsync([]);
        _userRepository.Setup(r => r.GetByIdAsync(readerId, default)).ReturnsAsync(readerUser);

        var result = await CreateSut().GetSectionDetailAsync(scene.Id, authorId);

        Assert.NotNull(result);
        Assert.True(result!.CommentAuthorNames.ContainsKey(readerId));
        Assert.Equal("Alice", result.CommentAuthorNames[readerId]);
    }

    [Fact]
    public async Task GetSectionDetailAsync_PopulatesReaderNamesFromReadEvents()
    {
        var project   = Project.Create("Book", "/path", Guid.NewGuid(), "root-uuid");
        var scene     = Section.CreateDocument(project.Id, "s1", "Scene 1", null, 0, "<p>a</p>", "h1", null);
        var authorId  = Guid.NewGuid();
        var reader1Id = Guid.NewGuid();
        var reader2Id = Guid.NewGuid();
        var reader1   = User.Create("r1@example.test", "Hilary", Role.BetaReader);
        var reader2   = User.Create("r2@example.test", "Becca",  Role.BetaReader);

        _sectionRepo.Setup(r => r.GetByIdAsync(scene.Id, default)).ReturnsAsync(scene);
        _commentService.Setup(s => s.GetThreadsForSectionAsync(scene.Id, authorId, default)).ReturnsAsync([]);
        _readEventRepository.Setup(r => r.GetBySectionIdAsync(scene.Id, default))
            .ReturnsAsync([ReadEvent.Create(scene.Id, reader1Id), ReadEvent.Create(scene.Id, reader2Id)]);
        _userRepository.Setup(r => r.GetByIdAsync(reader1Id, default)).ReturnsAsync(reader1);
        _userRepository.Setup(r => r.GetByIdAsync(reader2Id, default)).ReturnsAsync(reader2);

        var result = await CreateSut().GetSectionDetailAsync(scene.Id, authorId);

        Assert.NotNull(result);
        Assert.Equal(2, result!.ReaderNames.Count);
        Assert.Contains("Hilary", result.ReaderNames);
        Assert.Contains("Becca",  result.ReaderNames);
    }
}
