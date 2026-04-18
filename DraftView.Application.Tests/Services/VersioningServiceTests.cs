using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for VersioningService chapter/section republish and latest-version revoke behavior.
/// Covers: version creation, version numbering, IsPublished flag setting, validation rules,
/// change classification, AI summary wiring, and revoke invariants.
/// Excludes: reader content resolution and UI integration.
/// </summary>
public class VersioningServiceTests
{
    private readonly Mock<ISectionRepository> _sectionRepo;
    private readonly Mock<ISectionVersionRepository> _versionRepo;
    private readonly Mock<IHtmlDiffService> _htmlDiffService;
    private readonly Mock<IChangeClassificationService> _changeClassificationService;
    private readonly Mock<IAiSummaryService> _aiSummaryService;
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly VersioningService _sut;

    public VersioningServiceTests()
    {
        _sectionRepo = new Mock<ISectionRepository>();
        _versionRepo = new Mock<ISectionVersionRepository>();
        _htmlDiffService = new Mock<IHtmlDiffService>();
        _changeClassificationService = new Mock<IChangeClassificationService>();
        _aiSummaryService = new Mock<IAiSummaryService>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<SectionVersion>());
        _aiSummaryService.Setup(s => s.GenerateSummaryAsync(It.IsAny<string?>(), It.IsAny<string>(), default))
            .ReturnsAsync((string?)null);
        _sut = new VersioningService(
            _sectionRepo.Object,
            _versionRepo.Object,
            _htmlDiffService.Object,
            _changeClassificationService.Object,
            _aiSummaryService.Object,
            _unitOfWork.Object);
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

    [Fact]
    public async Task RepublishChapterAsync_SetsChangeClassification_WhenPreviousVersionExists()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);
        var previousVersion = SectionVersion.Create(doc, authorId, 1);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(1);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion> { previousVersion });

        _htmlDiffService.Setup(d => d.Compute(previousVersion.HtmlContent, doc.HtmlContent))
            .Returns(new List<DraftView.Domain.Diff.ParagraphDiffResult>());
        _changeClassificationService.Setup(c => c.Classify(It.IsAny<IReadOnlyList<DraftView.Domain.Diff.ParagraphDiffResult>>()))
            .Returns(ChangeClassification.Revision);

        SectionVersion? addedVersion = null;
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => addedVersion = v);

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        Assert.NotNull(addedVersion);
        Assert.Equal(ChangeClassification.Revision, addedVersion!.ChangeClassification);
    }

    [Fact]
    public async Task RepublishChapterAsync_DoesNotSetChangeClassification_WhenNoPreviousVersionExists()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(1);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion>());

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        _changeClassificationService.Verify(c =>
            c.Classify(It.IsAny<IReadOnlyList<DraftView.Domain.Diff.ParagraphDiffResult>>()), Times.Never);
    }

    [Fact]
    public async Task RepublishChapterAsync_StillPublishes_WhenClassificationFails()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);
        var previousVersion = SectionVersion.Create(doc, authorId, 1);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(1);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion> { previousVersion });

        _htmlDiffService.Setup(d => d.Compute(previousVersion.HtmlContent, doc.HtmlContent))
            .Returns(new List<DraftView.Domain.Diff.ParagraphDiffResult>());
        _changeClassificationService.Setup(c => c.Classify(It.IsAny<IReadOnlyList<DraftView.Domain.Diff.ParagraphDiffResult>>()))
            .Throws(new Exception("classification failed"));

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapterAsync_SetsAiSummary_WhenServiceReturnsSummary()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(0);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion>());
        _aiSummaryService.Setup(s => s.GenerateSummaryAsync(null, doc.HtmlContent!, default))
            .ReturnsAsync("Kira confronts Aldric in the library.");

        SectionVersion? addedVersion = null;
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => addedVersion = v);

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        Assert.NotNull(addedVersion);
        Assert.Equal("Kira confronts Aldric in the library.", addedVersion!.AiSummary);
    }

    [Fact]
    public async Task RepublishChapterAsync_DoesNotSetAiSummary_WhenServiceReturnsNull()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(0);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion>());
        _aiSummaryService.Setup(s => s.GenerateSummaryAsync(null, doc.HtmlContent!, default))
            .ReturnsAsync((string?)null);

        SectionVersion? addedVersion = null;
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => addedVersion = v);

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        Assert.NotNull(addedVersion);
        Assert.Null(addedVersion!.AiSummary);
    }

    [Fact]
    public async Task RepublishChapterAsync_StillPublishes_WhenAiSummaryServiceReturnsNull()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(0);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion>());
        _aiSummaryService.Setup(s => s.GenerateSummaryAsync(null, doc.HtmlContent!, default))
            .ReturnsAsync((string?)null);

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapterAsync_PassesPreviousHtmlToAiService_WhenPreviousVersionExists()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);
        var previousVersion = SectionVersion.Create(doc, authorId, 1);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(1);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion> { previousVersion });
        _htmlDiffService.Setup(d => d.Compute(previousVersion.HtmlContent, doc.HtmlContent))
            .Returns(new List<DraftView.Domain.Diff.ParagraphDiffResult>());
        _changeClassificationService.Setup(c => c.Classify(It.IsAny<IReadOnlyList<DraftView.Domain.Diff.ParagraphDiffResult>>()))
            .Returns(ChangeClassification.Polish);

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        _aiSummaryService.Verify(s => s.GenerateSummaryAsync(
            previousVersion.HtmlContent,
            doc.HtmlContent!,
            default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapterAsync_PassesNullPreviousHtml_WhenNoPreviousVersionExists()
    {
        var projectId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default))
            .ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default))
            .ReturnsAsync(0);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(doc.Id, default))
            .ReturnsAsync(new List<SectionVersion>());

        await _sut.RepublishChapterAsync(chapter.Id, authorId, default);

        _aiSummaryService.Verify(s => s.GenerateSummaryAsync(
            null,
            doc.HtmlContent!,
            default), Times.Once);
    }

    [Fact]
    public async Task RepublishSectionAsync_WithValidDocument_CreatesVersion()
    {
        var section = MakeDocument(Guid.NewGuid(), null);
        var authorId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(section.Id, default))
            .ReturnsAsync(0);

        await _sut.RepublishSectionAsync(section.Id, authorId, default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
    }

    [Fact]
    public async Task RepublishSectionAsync_WithFolderSection_ThrowsInvariantViolation()
    {
        var section = MakeChapter(Guid.NewGuid());

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishSectionAsync(section.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RepublishSectionAsync_WithSoftDeletedSection_ThrowsInvariantViolation()
    {
        var section = MakeDocument(Guid.NewGuid(), null);
        section.SoftDelete();

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishSectionAsync(section.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RepublishSectionAsync_WithNullHtmlContent_ThrowsInvariantViolation()
    {
        var section = Section.CreateDocumentForUpload(Guid.NewGuid(), "Scene 1", null, 0);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishSectionAsync(section.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RepublishSectionAsync_IncrementsVersionNumber()
    {
        var section = MakeDocument(Guid.NewGuid(), null);
        var authorId = Guid.NewGuid();
        SectionVersion? addedVersion = null;

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(section.Id, default))
            .ReturnsAsync(2);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => addedVersion = v);

        await _sut.RepublishSectionAsync(section.Id, authorId, default);

        Assert.NotNull(addedVersion);
        Assert.Equal(3, addedVersion!.VersionNumber);
    }

    [Fact]
    public async Task RepublishSectionAsync_SetsChangeClassification_WhenPreviousVersionExists()
    {
        var authorId = Guid.NewGuid();
        var section = MakeDocument(Guid.NewGuid(), null);
        var previousVersion = SectionVersion.Create(section, authorId, 1);
        SectionVersion? addedVersion = null;

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(section.Id, default))
            .ReturnsAsync(1);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(new List<SectionVersion> { previousVersion });
        _htmlDiffService.Setup(d => d.Compute(previousVersion.HtmlContent, section.HtmlContent))
            .Returns(new List<DraftView.Domain.Diff.ParagraphDiffResult>());
        _changeClassificationService.Setup(c => c.Classify(It.IsAny<IReadOnlyList<DraftView.Domain.Diff.ParagraphDiffResult>>()))
            .Returns(ChangeClassification.Revision);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => addedVersion = v);

        await _sut.RepublishSectionAsync(section.Id, authorId, default);

        Assert.NotNull(addedVersion);
        Assert.Equal(ChangeClassification.Revision, addedVersion!.ChangeClassification);
    }

    [Fact]
    public async Task RepublishSectionAsync_SetsAiSummary_WhenServiceReturnsSummary()
    {
        var section = MakeDocument(Guid.NewGuid(), null);
        var authorId = Guid.NewGuid();
        const string summaryText = "Summary text";
        SectionVersion? addedVersion = null;

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(section.Id, default))
            .ReturnsAsync(0);
        _aiSummaryService.Setup(s => s.GenerateSummaryAsync(null, section.HtmlContent!, default))
            .ReturnsAsync(summaryText);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<SectionVersion>(), default))
            .Callback<SectionVersion, CancellationToken>((v, _) => addedVersion = v);

        await _sut.RepublishSectionAsync(section.Id, authorId, default);

        Assert.NotNull(addedVersion);
        Assert.Equal(summaryText, addedVersion!.AiSummary);
    }

    [Fact]
    public async Task RepublishSectionAsync_StillPublishes_WhenClassificationFails()
    {
        var authorId = Guid.NewGuid();
        var section = MakeDocument(Guid.NewGuid(), null);
        var previousVersion = SectionVersion.Create(section, authorId, 1);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(section.Id, default))
            .ReturnsAsync(1);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(new List<SectionVersion> { previousVersion });
        _htmlDiffService.Setup(d => d.Compute(previousVersion.HtmlContent, section.HtmlContent))
            .Returns(new List<DraftView.Domain.Diff.ParagraphDiffResult>());
        _changeClassificationService.Setup(c => c.Classify(It.IsAny<IReadOnlyList<DraftView.Domain.Diff.ParagraphDiffResult>>()))
            .Throws(new Exception("classification failed"));

        await _sut.RepublishSectionAsync(section.Id, authorId, default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RevokeLatestVersionAsync_WithMultipleVersions_DeletesLatest()
    {
        var authorId = Guid.NewGuid();
        var section = MakeDocument(Guid.NewGuid(), null);
        var version1 = SectionVersion.Create(section, authorId, 1);
        var version2 = SectionVersion.Create(section, authorId, 2);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(new List<SectionVersion> { version1, version2 });

        await _sut.RevokeLatestVersionAsync(section.Id, authorId, default);

        _versionRepo.Verify(r => r.DeleteAsync(version2.Id, default), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RevokeLatestVersionAsync_WithNoVersions_ThrowsInvariantViolation()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(new List<SectionVersion>());

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RevokeLatestVersionAsync(section.Id, Guid.NewGuid(), default));

        Assert.Equal("I-VER-REVOKE-NONE", ex.InvariantCode);
    }

    [Fact]
    public async Task RevokeLatestVersionAsync_WithSingleVersion_ThrowsInvariantViolation()
    {
        var authorId = Guid.NewGuid();
        var section = MakeDocument(Guid.NewGuid(), null);
        var version = SectionVersion.Create(section, authorId, 1);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, default))
            .ReturnsAsync(new List<SectionVersion> { version });

        var ex = await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RevokeLatestVersionAsync(section.Id, authorId, default));

        Assert.Equal("I-VER-REVOKE-LAST", ex.InvariantCode);
    }

    [Fact]
    public async Task RevokeLatestVersionAsync_WithFolderSection_ThrowsInvariantViolation()
    {
        var section = MakeChapter(Guid.NewGuid());

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RevokeLatestVersionAsync(section.Id, Guid.NewGuid(), default));
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
