using Moq;
using ScrivenerSync.Application.Services;
using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Application.Tests.Services;

public class SyncServiceTests
{
    private readonly Mock<IScrivenerProjectRepository> _projectRepo = new();
    private readonly Mock<ISectionRepository>          _sectionRepo = new();
    private readonly Mock<IUnitOfWork>                 _unitOfWork  = new();
    private readonly Mock<IScrivenerProjectParser>     _parser      = new();
    private readonly Mock<IRtfConverter>               _converter   = new();

    private SyncService CreateSut() => new(
        _projectRepo.Object,
        _sectionRepo.Object,
        _unitOfWork.Object,
        _parser.Object,
        _converter.Object);

    private static ScrivenerProject MakeProject() =>
        ScrivenerProject.Create("Test Novel", "C:\\Dropbox\\Test.scriv");

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - project not found
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_WhenProjectNotFound_ThrowsEntityNotFoundException()
    {
        var sut = CreateSut();
        var missingId = Guid.NewGuid();

        _projectRepo.Setup(r => r.GetByIdAsync(missingId, default))
            .ReturnsAsync((ScrivenerProject?)null);

        await Assert.ThrowsAsync<Domain.Exceptions.EntityNotFoundException>(
            () => sut.ParseProjectAsync(missingId));
    }

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - new sections created
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_NewFolderNode_CreatesSectionWithFolderType()
    {
        var project = MakeProject();
        var sut = CreateSut();

        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new() { Uuid = "CHAP-001", Title = "Chapter 1", NodeType = ParsedNodeType.Folder, SortOrder = 0, Children = new() }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, It.IsAny<string>(), default))
            .ReturnsAsync((Section?)null);

        var addedSections = new List<Section>();
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback<Section, CancellationToken>((s, _) => addedSections.Add(s));

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());

        await sut.ParseProjectAsync(project.Id);

        Assert.Contains(addedSections, s => s.Title == "Chapter 1" && s.NodeType == NodeType.Folder);
    }

    [Fact]
    public async Task ParseProjectAsync_NewDocumentNode_CreatesSectionWithDocumentType()
    {
        var project = MakeProject();
        var sut = CreateSut();

        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new()
                {
                    Uuid            = "SCEN-001",
                    Title           = "Scene 1",
                    NodeType        = ParsedNodeType.Document,
                    ScrivenerStatus = "First Draft",
                    SortOrder       = 0,
                    Children        = new()
                }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, It.IsAny<string>(), default))
            .ReturnsAsync((Section?)null);

        var addedSections = new List<Section>();
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback<Section, CancellationToken>((s, _) => addedSections.Add(s));

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());

        _converter.Setup(c => c.ConvertAsync(It.IsAny<string>(), "SCEN-001", default))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>Content</p>", Hash = "abc123" });

        await sut.ParseProjectAsync(project.Id);

        Assert.Contains(addedSections, s => s.Title == "Scene 1" && s.NodeType == NodeType.Document);
    }

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - existing sections updated
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_ExistingSection_TitleChanged_UpdatesTitle()
    {
        var project = MakeProject();
        var sut = CreateSut();

        var existing = Section.CreateFolder(project.Id, "CHAP-001", "Old Title", null, 0);

        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new() { Uuid = "CHAP-001", Title = "New Title", NodeType = ParsedNodeType.Folder, SortOrder = 0, Children = new() }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "CHAP-001", default))
            .ReturnsAsync(existing);

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { existing });

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal("New Title", existing.Title);
    }

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - missing sections soft-deleted
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_SectionAbsentFromBinder_SoftDeletesSection()
    {
        var project = MakeProject();
        var sut = CreateSut();

        var orphan = Section.CreateFolder(project.Id, "GONE-001", "Deleted Chapter", null, 0);

        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { orphan });

        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(orphan.Id, default))
            .ReturnsAsync(new List<Section>());

        await sut.ParseProjectAsync(project.Id);

        Assert.True(orphan.IsSoftDeleted);
    }

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - sync status updated
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_OnSuccess_SetsSyncStatusHealthy()
    {
        var project = MakeProject();
        var sut = CreateSut();

        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001", Title = "Manuscript",
            NodeType = ParsedNodeType.Folder, Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal(SyncStatus.Healthy, project.SyncStatus);
    }

    [Fact]
    public async Task ParseProjectAsync_OnParseException_SetsSyncStatusError()
    {
        var project = MakeProject();
        var sut = CreateSut();

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _parser.Setup(p => p.Parse(It.IsAny<string>()))
            .Throws(new InvalidOperationException("File not found."));

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal(SyncStatus.Error, project.SyncStatus);
        Assert.NotNull(project.SyncErrorMessage);
    }

    // ---------------------------------------------------------------------------
    // DetectContentChangesAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DetectContentChangesAsync_WhenHashChanged_MarksContentChanged()
    {
        var project = MakeProject();
        var sut = CreateSut();

        var section = Section.CreateDocument(
            project.Id, "SCEN-001", "Scene 1", null, 0,
            "<p>Old</p>", "oldhash", "First Draft");
        section.Publish("oldhash");

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { section });

        _converter.Setup(c => c.ConvertAsync(It.IsAny<string>(), "SCEN-001", default))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>New</p>", Hash = "newhash" });

        await sut.DetectContentChangesAsync(project.Id);

        Assert.True(section.ContentChangedSincePublish);
    }

    [Fact]
    public async Task DetectContentChangesAsync_WhenHashUnchanged_DoesNotMarkContentChanged()
    {
        var project = MakeProject();
        var sut = CreateSut();

        var section = Section.CreateDocument(
            project.Id, "SCEN-001", "Scene 1", null, 0,
            "<p>Same</p>", "samehash", "First Draft");
        section.Publish("samehash");

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { section });

        _converter.Setup(c => c.ConvertAsync(It.IsAny<string>(), "SCEN-001", default))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>Same</p>", Hash = "samehash" });

        await sut.DetectContentChangesAsync(project.Id);

        Assert.False(section.ContentChangedSincePublish);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private void SetupParserWithTree(ScrivenerProject project, ParsedBinderNode root)
    {
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        _parser.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(new ParsedProject
            {
                ManuscriptRoot = root,
                StatusMap      = new Dictionary<string, string>()
            });
    }
}
