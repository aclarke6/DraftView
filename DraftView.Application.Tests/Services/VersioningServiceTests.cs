using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for VersioningService.RepublishChapterAsync.
/// Covers: version creation, version numbering, IsPublished flag setting, validation rules.
/// Excludes: RevokeLatestVersionAsync (V-Sprint 6), RepublishSectionAsync (V-Sprint 6),
/// reader content resolution (Phase 4), UI integration (Phase 5).
/// </summary>
public class VersioningServiceTests
{
    private readonly Mock<ISectionRepository> _sectionRepo;
    private readonly Mock<ISectionVersionRepository> _versionRepo;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly VersioningService _sut;

    public VersioningServiceTests()
    {
        _sectionRepo = new Mock<ISectionRepository>();
        _versionRepo = new Mock<ISectionVersionRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _sut = new VersioningService(_sectionRepo.Object, _versionRepo.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task RepublishChapterAsync_WithValidChapter_CreatesVersionPerDocument()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc1 = MakeDocument(projectId, chapter.Id);
        var doc2 = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc1, doc2 });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(0);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Exactly(2));
    }

    [Fact]
    public async Task RepublishChapterAsync_WithValidChapter_SetsIsPublishedOnEachDocument()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc1 = MakeDocument(projectId, chapter.Id);
        var doc2 = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc1, doc2 });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(0);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        Assert.True(doc1.IsPublished);
        Assert.True(doc2.IsPublished);
    }

    [Fact]
    public async Task RepublishChapterAsync_WithValidChapter_VersionNumberStartsAtOne()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(0);

        SectionVersion? capturedVersion = null;
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => capturedVersion = v);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        Assert.NotNull(capturedVersion);
        Assert.Equal(1, capturedVersion.VersionNumber);
    }

    [Fact]
    public async Task RepublishChapterAsync_VersionNumberIncrements_WhenVersionsAlreadyExist()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(2);

        SectionVersion? capturedVersion = null;
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => capturedVersion = v);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        Assert.NotNull(capturedVersion);
        Assert.Equal(3, capturedVersion.VersionNumber);
    }

    [Fact]
    public async Task RepublishChapterAsync_WithNoDocuments_ThrowsInvariantViolation()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default));
        Assert.Equal("I-VER-NO-DOCS", ex.InvariantCode);
    }

    [Fact]
    public async Task RepublishChapterAsync_WithFolderSection_ThrowsInvariantViolation()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var document = MakeDocument(projectId, null);

        _sectionRepo.Setup(r => r.GetByIdAsync(document.Id, default))
            .ReturnsAsync(document);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishChapterAsync(document.Id, Guid.NewGuid(), default));
        Assert.Equal("I-VER-CHAPTER", ex.InvariantCode);
    }

    [Fact]
    public async Task RepublishChapterAsync_IgnoresSoftDeletedDocuments()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc1 = MakeDocument(projectId, chapter.Id);
        var doc2 = MakeDocument(projectId, chapter.Id);
        doc2.SoftDelete();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc1, doc2 });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc1.Id, default))
            .ReturnsAsync(0);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapterAsync_WorksForManualProject()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeManualDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(0);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapterAsync_SavesOnce()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc1 = MakeDocument(projectId, chapter.Id);
        var doc2 = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc1, doc2 });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(0);

        // Act
        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        // Assert
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // Test helpers
    private static Section MakeChapter(Guid projectId) =>
        Section.CreateFolder(projectId, Guid.NewGuid().ToString(), "Chapter 1", null, 0);

    private static Section MakeDocument(Guid projectId, Guid? chapterId) =>
        Section.CreateDocument(projectId, Guid.NewGuid().ToString(),
            "Scene 1", chapterId, 0, "<p>content</p>", "hash", null);

    private static Section MakeManualDocument(Guid projectId, Guid chapterId)
    {
        var section = Section.CreateDocumentForUpload(projectId, "Scene 1", chapterId, 0);
        section.UpdateContent("<p>content</p>", "hash");
        return section;
    }
}
