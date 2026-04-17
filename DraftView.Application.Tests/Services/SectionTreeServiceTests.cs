using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for SectionTreeService covering upload section creation and tree shaping.
/// Excludes UI concerns and versioning responsibilities.
/// </summary>
public class SectionTreeServiceTests
{
    private readonly Mock<ISectionRepository> sectionRepository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();

    private SectionTreeService CreateSut() => new(sectionRepository.Object, unitOfWork.Object);

    private static Section Folder(Guid projectId, string title, Guid? parentId, int sortOrder) =>
        Section.CreateFolder(projectId, Guid.NewGuid().ToString(), title, parentId, sortOrder);

    private static Section Document(Guid projectId, string title, Guid? parentId, int sortOrder) =>
        Section.CreateDocument(projectId, Guid.NewGuid().ToString(), title, parentId, sortOrder, "<p>x</p>", "hash", null);

    /// <summary>Creates a section when no matching upload section exists.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_CreatesSection_WhenNoneExists()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(Array.Empty<Section>());
        sectionRepository.Setup(r => r.AddAsync(It.IsAny<Section>(), default)).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "Intro", null, null);

        Assert.Equal(projectId, section.ProjectId);
        sectionRepository.Verify(r => r.AddAsync(It.IsAny<Section>(), default), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    /// <summary>Returns an existing section when title and parent match.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_ReturnsExisting_WhenTitleAndParentMatch()
    {
        var projectId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var existing = Document(projectId, "Intro", parentId, 0);
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(new List<Section> { existing });
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "  intro  ", parentId, null);

        Assert.Same(existing, section);
    }

    /// <summary>Matching is case-insensitive.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_IsCaseInsensitive_WhenMatchingTitle()
    {
        var projectId = Guid.NewGuid();
        var existing = Document(projectId, "Intro", null, 0);
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(new List<Section> { existing });
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "intro", null, null);

        Assert.Same(existing, section);
    }

    /// <summary>Service should not create duplicates when a matching section exists.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_NeverCreatesDuplicate()
    {
        var projectId = Guid.NewGuid();
        var existing = Document(projectId, "Intro", null, 0);
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(new List<Section> { existing });
        var sut = CreateSut();

        await sut.GetOrCreateForUploadAsync(projectId, "Intro", null, null);

        sectionRepository.Verify(r => r.AddAsync(It.IsAny<Section>(), default), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    /// <summary>New upload sections should not have a Scrivener UUID.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_CreatedSection_HasNullScrivenerUuid()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(Array.Empty<Section>());
        sectionRepository.Setup(r => r.AddAsync(It.IsAny<Section>(), default)).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "Intro", null, null);

        Assert.Null(section.ScrivenerUuid);
    }

    /// <summary>New upload sections should be documents.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_CreatedSection_IsDocument()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(Array.Empty<Section>());
        sectionRepository.Setup(r => r.AddAsync(It.IsAny<Section>(), default)).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "Intro", null, null);

        Assert.Equal(NodeType.Document, section.NodeType);
    }

    /// <summary>Default sort order should append to the end of siblings.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_DefaultsSortOrder_ToEndOfSiblingList()
    {
        var projectId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var siblingA = Document(projectId, "A", parentId, 0);
        var siblingB = Document(projectId, "B", parentId, 2);
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(new List<Section> { siblingA, siblingB });
        sectionRepository.Setup(r => r.AddAsync(It.IsAny<Section>(), default)).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "Intro", parentId, null);

        Assert.Equal(3, section.SortOrder);
    }

    /// <summary>Explicit sort order should be respected.</summary>
    [Fact]
    public async Task GetOrCreateForUploadAsync_UsesSortOrder_WhenSupplied()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(Array.Empty<Section>());
        sectionRepository.Setup(r => r.AddAsync(It.IsAny<Section>(), default)).Returns(Task.CompletedTask);
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        var section = await sut.GetOrCreateForUploadAsync(projectId, "Intro", null, 7);

        Assert.Equal(7, section.SortOrder);
    }

    /// <summary>Tree rendering should return a sorted hierarchy.</summary>
    [Fact]
    public async Task GetTreeAsync_ReturnsSortedHierarchy()
    {
        var projectId = Guid.NewGuid();
        var rootA = Folder(projectId, "A", null, 1);
        var rootB = Folder(projectId, "B", null, 0);
        var child = Document(projectId, "Child", rootB.Id, 0);
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(new List<Section> { rootA, child, rootB });
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(projectId);

        Assert.Equal("B", tree[0].Title);
        Assert.Equal("A", tree[1].Title);
        Assert.Single(tree[0].Children);
        Assert.Equal("Child", tree[0].Children[0].Title);
    }

    /// <summary>Soft-deleted sections should not appear in the tree.</summary>
    [Fact]
    public async Task GetTreeAsync_ExcludesSoftDeletedSections()
    {
        var projectId = Guid.NewGuid();
        var root = Folder(projectId, "A", null, 0);
        var deleted = Document(projectId, "Deleted", root.Id, 0);
        deleted.SoftDelete();
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(new List<Section> { root, deleted });
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(projectId);

        Assert.Single(tree);
        Assert.Empty(tree[0].Children);
    }

    /// <summary>An empty project should return an empty tree.</summary>
    [Fact]
    public async Task GetTreeAsync_ReturnsEmptyList_WhenNoSections()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByProjectIdAsync(projectId, default)).ReturnsAsync(Array.Empty<Section>());
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(projectId);

        Assert.Empty(tree);
    }
}
