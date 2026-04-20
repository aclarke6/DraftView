using Moq;
using Microsoft.Extensions.Logging;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Tests.Services;

public class ScrivenerSyncServiceTests
{
    private static readonly Guid ValidAuthorId = Guid.NewGuid();

    private readonly Mock<IProjectRepository>   _projectRepo       = new();
    private readonly Mock<ISectionRepository>            _sectionRepo       = new();
    private readonly Mock<IUnitOfWork>                   _unitOfWork        = new();
    private readonly Mock<IScrivenerProjectParser>       _parser            = new();
    private readonly Mock<IRtfConverter>                 _converter         = new();
    private readonly Mock<ILocalPathResolver>            _pathResolver      = new();
    private readonly Mock<ISyncProgressTracker>          _progressTracker   = new();
    private readonly Mock<IDropboxConnectionChecker>     _connectionChecker = new();
    private readonly Mock<IDropboxClientFactory>         _clientFactory     = new();
    private readonly Mock<IDropboxFileDownloader>        _fileDownloader    = new();
    private readonly Mock<ILogger<ScrivenerSyncService>>          _logger            = new();
    private readonly Mock<IAuthorNotificationRepository> _notificationRepo  = new();
    private readonly Mock<IUserRepository>               _userRepo          = new();

    private ScrivenerSyncService CreateSut() => new(
        _projectRepo.Object,
        _sectionRepo.Object,
        _unitOfWork.Object,
        _parser.Object,
        _converter.Object,
        _pathResolver.Object,
        _progressTracker.Object,
        _connectionChecker.Object,
        _clientFactory.Object,
        _fileDownloader.Object,
        _logger.Object,
        _notificationRepo.Object,
        _userRepo.Object);

    public ScrivenerSyncServiceTests()
    {
        _connectionChecker.Setup(x => x.IsConnectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _connectionChecker.Setup(x => x.SetUserId(It.IsAny<Guid>()));
        _pathResolver.Setup(x => x.SetUserId(It.IsAny<Guid>()));
        _parser.Setup(p => p.Parse(It.IsAny<string>())).Returns(new ParsedProject
        {
            ManuscriptRoot = new ParsedBinderNode
            {
                Uuid = "ROOT-DEFAULT",
                Title = "Manuscript",
                NodeType = ParsedNodeType.Folder,
                Children = new()
            },
            StatusMap = new Dictionary<string, string>()
        });
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Section>());
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), It.IsAny<CancellationToken>()));
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Section>());
        _fileDownloader.Setup(x => x.ListAllEntriesWithCursorAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<DropboxChangedEntry>(), "initial-cursor"));
        _fileDownloader.Setup(x => x.DownloadChangedEntriesAsync(
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<DropboxChangedEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/fake/path");
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<DropboxChangedEntry>(), "next-cursor"));
    }

    private static Project MakeProject() =>
        Project.Create("Test Novel", "/Apps/Scrivener/Test.scriv", ValidAuthorId);

    private void SetupPathResolver(Project project, string localPath = "/fake/path")
    {
        _pathResolver.Setup(r => r.ResolveAsync(project, default))
            .ReturnsAsync(localPath);
        _pathResolver.Setup(r => r.ResolveScrivxAsync(project, default))
            .ReturnsAsync(localPath + "/Test.scrivx");
    }

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - project not found
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_WhenProjectNotFound_ThrowsEntityNotFoundException()
    {
        var sut       = CreateSut();
        var missingId = Guid.NewGuid();

        _projectRepo.Setup(r => r.GetByIdAsync(missingId, default))
            .ReturnsAsync((Project?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.ParseProjectAsync(missingId));
    }

    // ---------------------------------------------------------------------------
    // ParseProjectAsync - new sections created
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_NewFolderNode_CreatesSectionWithFolderType()
    {
        var project = MakeProject();
        var sut     = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new() { Uuid = "CHAP-001", Title = "Chapter 1",
                    NodeType = ParsedNodeType.Folder, SortOrder = 0, Children = new() }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, It.IsAny<string>(), default))
            .ReturnsAsync((Section?)null);

        var addedSections = new List<Section>();
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback<Section, CancellationToken>((s, _) => addedSections.Add(s));

        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());

        await sut.ParseProjectAsync(project.Id);

        Assert.Contains(addedSections, s => s.Title == "Chapter 1" && s.NodeType == NodeType.Folder);
    }

    [Fact]
    public async Task ParseProjectAsync_NewDocumentNode_CreatesSectionWithDocumentType()
    {
        var project = MakeProject();
        var sut     = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new() { Uuid = "SCEN-001", Title = "Scene 1",
                    NodeType = ParsedNodeType.Document,
                    ScrivenerStatus = "First Draft", SortOrder = 0, Children = new() }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, It.IsAny<string>(), default))
            .ReturnsAsync((Section?)null);

        var addedSections = new List<Section>();
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback<Section, CancellationToken>((s, _) => addedSections.Add(s));

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
        var project  = MakeProject();
        var sut      = CreateSut();
        var existing = Section.CreateFolder(project.Id, "CHAP-001", "Old Title", null, 0);

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid     = "ROOT-001",
            Title    = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new() { Uuid = "CHAP-001", Title = "New Title",
                    NodeType = ParsedNodeType.Folder, SortOrder = 0, Children = new() }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "CHAP-001", default))
            .ReturnsAsync(existing);
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
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
        var sut     = CreateSut();
        var orphan  = Section.CreateFolder(project.Id, "GONE-001", "Deleted Chapter", null, 0);

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001", Title = "Manuscript",
            NodeType = ParsedNodeType.Folder, Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
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
        var sut     = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001", Title = "Manuscript",
            NodeType = ParsedNodeType.Folder, Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal(SyncStatus.Healthy, project.SyncStatus);
    }

    [Fact]
    public async Task ParseProjectAsync_WithNullCursor_PerformsFullSyncAndStoresCursor()
    {
        var project = MakeProject();
        var sut = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001",
            Title = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
        _fileDownloader.Setup(x => x.ListAllEntriesWithCursorAsync(project.AuthorId, project.DropboxPath, default))
            .ReturnsAsync((new List<DropboxChangedEntry>
            {
                new("/apps/scrivener/test.scriv/files/data/a/content.rtf", DropboxEntryType.Added, "h1")
            }, "cursor-full-001"));

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal("cursor-full-001", project.DropboxCursor);
        _fileDownloader.Verify(x => x.ListAllEntriesWithCursorAsync(project.AuthorId, project.DropboxPath, default), Times.Once);
    }

    [Fact]
    public async Task ParseProjectAsync_WithExistingCursor_ProcessesOnlyChangedEntries()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");

        var section = Section.CreateDocument(project.Id, "SCEN-001", "Scene 1", null, 0, "<p>Old</p>", "oldhash", "First Draft");
        var sut = CreateSut();

        SetupPathResolver(project);
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "SCEN-001", default)).ReturnsAsync(section);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(It.IsAny<Guid>(), default)).ReturnsAsync(new List<Section>());
        _converter.Setup(c => c.ConvertAsync("/fake/path", "SCEN-001", default))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>New</p>", Hash = "newhash" });
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ReturnsAsync((new List<DropboxChangedEntry>
            {
                new("/apps/scrivener/test.scriv/files/data/SCEN-001/content.rtf", DropboxEntryType.Modified, "newhash")
            }, "cursor-new"));

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal("cursor-new", project.DropboxCursor);
        Assert.Equal("newhash", section.ContentHash);
        _fileDownloader.Verify(x => x.ListAllEntriesWithCursorAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParseProjectAsync_WhenCursorExpired_FallsBackToFullSync()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");

        var sut = CreateSut();
        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001",
            Title = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ThrowsAsync(new InvalidOperationException("reset_cursor"));
        _fileDownloader.Setup(x => x.ListAllEntriesWithCursorAsync(project.AuthorId, project.DropboxPath, default))
            .ReturnsAsync((new List<DropboxChangedEntry>(), "cursor-recovered"));

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal("cursor-recovered", project.DropboxCursor);
        _fileDownloader.Verify(x => x.ListAllEntriesWithCursorAsync(project.AuthorId, project.DropboxPath, default), Times.Once);
    }

    [Fact]
    public async Task ParseProjectAsync_WithDeletedEntry_SoftDeletesMatchingSection()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");

        var section = Section.CreateDocument(project.Id, "SCEN-DELETE", "Scene Delete", null, 0, "<p>Old</p>", "oldhash", "Draft");
        var sut = CreateSut();

        SetupPathResolver(project);
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "SCEN-DELETE", default)).ReturnsAsync(section);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(section.Id, default)).ReturnsAsync(new List<Section>());
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ReturnsAsync((new List<DropboxChangedEntry>
            {
                new("/apps/scrivener/test.scriv/files/data/SCEN-DELETE/content.rtf", DropboxEntryType.Deleted, null)
            }, "cursor-new"));

        await sut.ParseProjectAsync(project.Id);

        Assert.True(section.IsSoftDeleted);
        Assert.Equal("cursor-new", project.DropboxCursor);
    }

    [Fact]
    public async Task ParseProjectAsync_UpdatesDropboxCursor_AfterSuccessfulSync()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");
        var sut = CreateSut();

        SetupPathResolver(project);
        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ReturnsAsync((new List<DropboxChangedEntry>(), "cursor-updated"));

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal("cursor-updated", project.DropboxCursor);
    }

    [Fact]
    public async Task ParseProjectAsync_WithExistingCursor_NewBinderItemInScrivx_CreatesNewSection()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");
        var sut = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001",
            Title = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new()
                {
                    Uuid = "SCEN-NEW-001",
                    Title = "New Scene",
                    NodeType = ParsedNodeType.Document,
                    SortOrder = 0,
                    Children = new()
                }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "SCEN-NEW-001", default))
            .ReturnsAsync((Section?)null);
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ReturnsAsync((new List<DropboxChangedEntry>
            {
                new("/apps/scrivener/test.scriv/files/data/SCEN-UNCHANGED/content.rtf", DropboxEntryType.Modified, "h1")
            }, "cursor-new"));

        var addedSections = new List<Section>();
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback<Section, CancellationToken>((s, _) => addedSections.Add(s));

        await sut.ParseProjectAsync(project.Id);

        Assert.Contains(addedSections, s => s.ScrivenerUuid == "SCEN-NEW-001");
    }

    [Fact]
    public async Task ParseProjectAsync_WithExistingCursor_CreatesNewSection_WhenScrivxNotInChangedEntries()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");
        var sut = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001",
            Title = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new()
                {
                    Uuid = "SCEN-NEW-002",
                    Title = "Scene Without Scrivx Entry",
                    NodeType = ParsedNodeType.Document,
                    SortOrder = 0,
                    Children = new()
                }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "SCEN-NEW-002", default))
            .ReturnsAsync((Section?)null);
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ReturnsAsync((new List<DropboxChangedEntry>
            {
                new("/apps/scrivener/test.scriv/files/data/OTHER-UUID/content.rtf", DropboxEntryType.Modified, "h2")
            }, "cursor-new"));

        var addedSections = new List<Section>();
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback<Section, CancellationToken>((s, _) => addedSections.Add(s));

        await sut.ParseProjectAsync(project.Id);

        Assert.Contains(addedSections, s => s.ScrivenerUuid == "SCEN-NEW-002");
    }

    [Fact]
    public async Task ParseProjectAsync_WithExistingCursor_NewSiblingAdded_DoesNotDuplicateExistingSections()
    {
        var project = MakeProject();
        project.UpdateDropboxCursor("cursor-old");
        var sut = CreateSut();
        var existingRoot = Section.CreateFolder(project.Id, "ROOT-001", "Manuscript", null, 0);
        var existingSibling = Section.CreateDocument(project.Id, "SCEN-EXIST-001", "Existing Scene", existingRoot.Id, 0, "<p>x</p>", "h0", null);

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001",
            Title = "Manuscript",
            NodeType = ParsedNodeType.Folder,
            Children = new List<ParsedBinderNode>
            {
                new()
                {
                    Uuid = "SCEN-EXIST-001",
                    Title = "Existing Scene",
                    NodeType = ParsedNodeType.Document,
                    SortOrder = 0,
                    Children = new()
                },
                new()
                {
                    Uuid = "SCEN-NEW-003",
                    Title = "New Sibling Scene",
                    NodeType = ParsedNodeType.Document,
                    SortOrder = 1,
                    Children = new()
                }
            }
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync(existingRoot);
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "SCEN-EXIST-001", default))
            .ReturnsAsync(existingSibling);
        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "SCEN-NEW-003", default))
            .ReturnsAsync((Section?)null);
        _fileDownloader.Setup(x => x.ListChangedEntriesAsync(project.AuthorId, "cursor-old", default))
            .ReturnsAsync((new List<DropboxChangedEntry>
            {
                new("/apps/scrivener/test.scriv/files/data/SCEN-EXIST-001/content.rtf", DropboxEntryType.Modified, "h-existing")
            }, "cursor-new"));

        var addCount = 0;
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default))
            .Callback(() => addCount++);

        await sut.ParseProjectAsync(project.Id);

        Assert.Equal(1, addCount);
    }

    [Fact]
    public async Task ParseProjectAsync_OnParseException_SetsSyncStatusError()
    {
        var project = MakeProject();
        var sut     = CreateSut();

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default))
            .ReturnsAsync(project);

        SetupPathResolver(project);

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
        var sut     = CreateSut();
        var section = Section.CreateDocument(project.Id, "SCEN-001", "Scene 1",
            null, 0, "<p>Old</p>", "oldhash", "First Draft");
        section.PublishAsPartOfChapter("oldhash");

        SetupPathResolver(project, "/fake/path");

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { section });
        _converter.Setup(c => c.ConvertAsync("/fake/path", "SCEN-001", default))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>New</p>", Hash = "newhash" });

        await sut.DetectContentChangesAsync(project.Id);

        Assert.True(section.ContentChangedSincePublish);
    }

    [Fact]
    public async Task DetectContentChangesAsync_WhenHashUnchanged_DoesNotMarkContentChanged()
    {
        var project = MakeProject();
        var sut     = CreateSut();
        var section = Section.CreateDocument(project.Id, "SCEN-001", "Scene 1",
            null, 0, "<p>Same</p>", "samehash", "First Draft");
        section.PublishAsPartOfChapter("samehash");

        SetupPathResolver(project, "/fake/path");

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section> { section });
        _converter.Setup(c => c.ConvertAsync("/fake/path", "SCEN-001", default))
            .ReturnsAsync(new RtfConversionResult { Html = "<p>Same</p>", Hash = "samehash" });

        await sut.DetectContentChangesAsync(project.Id);

        Assert.False(section.ContentChangedSincePublish);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    // ---------------------------------------------------------------------------
    // Notifications
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ParseProjectAsync_WritesNotification_OnSuccessfulSync()
    {
        var project = MakeProject();
        var author  = User.Create("author@example.com", "Author", Role.Author);
        var sut     = CreateSut();

        SetupPathResolver(project);
        SetupParserWithTree(project, new ParsedBinderNode
        {
            Uuid = "ROOT-001", Title = "Manuscript",
            NodeType = ParsedNodeType.Folder, Children = new()
        });

        _sectionRepo.Setup(r => r.GetByScrivenerUuidAsync(project.Id, "ROOT-001", default))
            .ReturnsAsync((Section?)null);
        _sectionRepo.Setup(r => r.AddAsync(It.IsAny<Section>(), default));
        _sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, default))
            .ReturnsAsync(new List<Section>());
        _userRepo.Setup(r => r.GetAuthorAsync(default)).ReturnsAsync(author);

        await sut.ParseProjectAsync(project.Id);

        _notificationRepo.Verify(
            r => r.AddAsync(It.Is<AuthorNotification>(n =>
                n.AuthorId == author.Id &&
                n.Title.Contains(project.Name)),
                default),
            Times.Once);
    }

    [Fact]
    public async Task ParseProjectAsync_DoesNotWriteNotification_OnSyncFailure()
    {
        var project = MakeProject();
        var sut     = CreateSut();

        _projectRepo.Setup(r => r.GetByIdAsync(project.Id, default)).ReturnsAsync(project);
        SetupPathResolver(project);
        _parser.Setup(p => p.Parse(It.IsAny<string>()))
            .Throws(new InvalidOperationException("File not found."));

        await sut.ParseProjectAsync(project.Id);

        _notificationRepo.Verify(
            r => r.AddAsync(It.IsAny<AuthorNotification>(), default),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private void SetupParserWithTree(Project project, ParsedBinderNode root)
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
