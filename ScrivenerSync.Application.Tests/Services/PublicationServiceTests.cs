using Moq;
using ScrivenerSync.Application.Services;
using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Exceptions;
using ScrivenerSync.Domain.Interfaces.Repositories;

namespace ScrivenerSync.Application.Tests.Services;

public class PublicationServiceTests
{
    private readonly Mock<ISectionRepository>          _sectionRepo = new();
    private readonly Mock<IScrivenerProjectRepository> _projectRepo = new();
    private readonly Mock<IUnitOfWork>                 _unitOfWork  = new();

    private PublicationService CreateSut() => new(
        _sectionRepo.Object,
        _projectRepo.Object,
        _unitOfWork.Object);

    private static Section MakeDocument(Guid projectId, bool published = false)
    {
        var s = Section.CreateDocument(projectId, Guid.NewGuid().ToString(),
            "Scene 1", null, 0, "<p>Content</p>", "hash123", "First Draft");
        if (published) s.Publish("hash123");
        return s;
    }

    private static User MakeAuthor() =>
        User.Create("author@example.com", "Author", Role.Author);

    // ---------------------------------------------------------------------------
    // Publish
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_ValidDocument_SetsIsPublished()
    {
        var projectId = Guid.NewGuid();
        var author    = MakeAuthor();
        var section   = MakeDocument(projectId);
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(new List<Section>());

        await sut.PublishAsync(section.Id, author.Id);

        Assert.True(section.IsPublished);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_SectionNotFound_ThrowsEntityNotFoundException()
    {
        var sut = CreateSut();
        var missingId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(missingId, default))
            .ReturnsAsync((Section?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.PublishAsync(missingId, Guid.NewGuid()));
    }

    [Fact]
    public async Task PublishAsync_FolderNode_ThrowsInvariantViolationException()
    {
        var sut     = CreateSut();
        var section = Section.CreateFolder(Guid.NewGuid(), "UUID-1", "Chapter 1", null, 0);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => sut.PublishAsync(section.Id, Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------------
    // Unpublish
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UnpublishAsync_PublishedSection_SetsIsPublishedFalse()
    {
        var projectId = Guid.NewGuid();
        var section   = MakeDocument(projectId, published: true);
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);

        await sut.UnpublishAsync(section.Id, Guid.NewGuid());

        Assert.False(section.IsPublished);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UnpublishAsync_SectionNotFound_ThrowsEntityNotFoundException()
    {
        var sut       = CreateSut();
        var missingId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(missingId, default))
            .ReturnsAsync((Section?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => sut.UnpublishAsync(missingId, Guid.NewGuid()));
    }

    // ---------------------------------------------------------------------------
    // GetPublishedSections
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetPublishedSectionsAsync_ReturnsPublishedSections()
    {
        var projectId = Guid.NewGuid();
        var sections  = new List<Section> { MakeDocument(projectId, published: true) };
        var sut       = CreateSut();

        _sectionRepo.Setup(r => r.GetPublishedByProjectIdAsync(projectId, default))
            .ReturnsAsync(sections);

        var result = await sut.GetPublishedSectionsAsync(projectId);

        Assert.Single(result);
    }
}
