using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Policies;
using Microsoft.Extensions.Configuration;
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
    private readonly Mock<IConfiguration> _configuration;
    private readonly VersioningService _sut;

    public VersioningServiceTests()
    {
        _sectionRepo = new Mock<ISectionRepository>();
        _versionRepo = new Mock<ISectionVersionRepository>();
        _htmlDiffService = new Mock<IHtmlDiffService>();
        _changeClassificationService = new Mock<IChangeClassificationService>();
        _aiSummaryService = new Mock<IAiSummaryService>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _configuration = new Mock<IConfiguration>();
        _configuration.Setup(c => c["DraftView:SubscriptionTier"])
            .Returns((string?)null);
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
            _unitOfWork.Object,
            _configuration.Object);
    }

    [Fact]
    public async Task RepublishChapterAsync_WhenAtRetentionLimit_ThrowsVersionRetentionLimitException()
    {
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Free");
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default)).ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetVersionCountAsync(doc.Id, default)).ReturnsAsync(VersionRetentionPolicy.FreeLimit);

        var ex = await Assert.ThrowsAsync<VersionRetentionLimitException>(
            () => _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default));

        Assert.Equal(VersionRetentionPolicy.FreeLimit, ex.Limit);
    }

    [Fact]
    public async Task RepublishChapterAsync_WhenBelowRetentionLimit_CreatesVersion()
    {
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Free");
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default)).ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetVersionCountAsync(doc.Id, default)).ReturnsAsync(VersionRetentionPolicy.FreeLimit - 1);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default)).ReturnsAsync(0);

        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
    }

    [Fact]
    public async Task RepublishSectionAsync_WhenTierConfigIsInvalid_DefaultsToFreeLimit()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("UnknownTier");
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetVersionCountAsync(section.Id, default)).ReturnsAsync(VersionRetentionPolicy.FreeLimit);

        var ex = await Assert.ThrowsAsync<VersionRetentionLimitException>(
            () => _sut.RepublishSectionAsync(section.Id, Guid.NewGuid(), default));

        Assert.Equal(VersionRetentionPolicy.FreeLimit, ex.Limit);
    }

    [Fact]
    public async Task RepublishSectionAsync_WhenAtRetentionLimit_ThrowsVersionRetentionLimitException()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Free");
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetVersionCountAsync(section.Id, default)).ReturnsAsync(VersionRetentionPolicy.FreeLimit);

        var ex = await Assert.ThrowsAsync<VersionRetentionLimitException>(
            () => _sut.RepublishSectionAsync(section.Id, Guid.NewGuid(), default));

        Assert.Equal(VersionRetentionPolicy.FreeLimit, ex.Limit);
    }

    [Fact]
    public async Task RepublishSectionAsync_WhenBelowRetentionLimit_CreatesVersion()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Free");
        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        _versionRepo.Setup(r => r.GetVersionCountAsync(section.Id, default)).ReturnsAsync(VersionRetentionPolicy.FreeLimit - 1);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(section.Id, default)).ReturnsAsync(0);

        await _sut.RepublishSectionAsync(section.Id, Guid.NewGuid(), default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapterAsync_WhenTierIsUltimate_NeverThrowsRetentionException()
    {
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        var doc = MakeDocument(projectId, chapter.Id);

        _configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Ultimate");
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default)).ReturnsAsync(chapter);
        _sectionRepo.Setup(r => r.GetAllDescendantsAsync(chapter.Id, default)).ReturnsAsync(new List<Section> { doc });
        _versionRepo.Setup(r => r.GetVersionCountAsync(doc.Id, default)).ReturnsAsync(int.MaxValue);
        _versionRepo.Setup(r => r.GetMaxVersionNumberAsync(doc.Id, default)).ReturnsAsync(0);

        await _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default);

        _versionRepo.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Once);
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

    [Fact]
    public async Task LockChapterAsync_SetsIsLocked()
    {
        var chapter = MakeChapter(Guid.NewGuid());

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await _sut.LockChapterAsync(chapter.Id, Guid.NewGuid(), default);

        Assert.True(chapter.IsLocked);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task LockChapterAsync_WithNonFolder_ThrowsInvariantViolation()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.LockChapterAsync(section.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task LockChapterAsync_WithMissingSection_ThrowsEntityNotFoundException()
    {
        var chapterId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapterId, default))
            .ReturnsAsync((Section?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.LockChapterAsync(chapterId, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task UnlockChapterAsync_ClearsIsLocked()
    {
        var chapter = MakeChapter(Guid.NewGuid());
        chapter.Lock();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await _sut.UnlockChapterAsync(chapter.Id, Guid.NewGuid(), default);

        Assert.False(chapter.IsLocked);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task UnlockChapterAsync_WithNonFolder_ThrowsInvariantViolation()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.UnlockChapterAsync(section.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task UnlockChapterAsync_WhenNotLocked_ThrowsInvariantViolation()
    {
        var chapter = MakeChapter(Guid.NewGuid());

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.UnlockChapterAsync(chapter.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RepublishChapterAsync_WhenLocked_ThrowsInvariantViolation()
    {
        var chapter = MakeChapter(Guid.NewGuid());
        chapter.Lock();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishChapterAsync(chapter.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RepublishSectionAsync_WhenParentChapterLocked_ThrowsInvariantViolation()
    {
        var projectId = Guid.NewGuid();
        var chapter = MakeChapter(projectId);
        chapter.Lock();
        var document = MakeDocument(projectId, chapter.Id);

        _sectionRepo.Setup(r => r.GetByIdAsync(document.Id, default))
            .ReturnsAsync(document);
        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.RepublishSectionAsync(document.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task ScheduleChapterAsync_SetsScheduledPublishAt()
    {
        var chapter = MakeChapter(Guid.NewGuid());
        var scheduledAt = DateTime.UtcNow.Date.AddDays(1);

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await _sut.ScheduleChapterAsync(chapter.Id, Guid.NewGuid(), scheduledAt, default);

        Assert.Equal(scheduledAt, chapter.ScheduledPublishAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ScheduleChapterAsync_WithNonFolder_ThrowsInvariantViolation()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.ScheduleChapterAsync(section.Id, Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(1), default));
    }

    [Fact]
    public async Task ScheduleChapterAsync_WithMissingSection_ThrowsEntityNotFoundException()
    {
        var chapterId = Guid.NewGuid();

        _sectionRepo.Setup(r => r.GetByIdAsync(chapterId, default))
            .ReturnsAsync((Section?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _sut.ScheduleChapterAsync(chapterId, Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(1), default));
    }

    [Fact]
    public async Task ScheduleChapterAsync_WithPastDate_ThrowsInvariantViolation()
    {
        var chapter = MakeChapter(Guid.NewGuid());

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.ScheduleChapterAsync(chapter.Id, Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(-1), default));
    }

    [Fact]
    public async Task ClearScheduleAsync_ClearsScheduledPublishAt()
    {
        var chapter = MakeChapter(Guid.NewGuid());
        chapter.SchedulePublish(DateTime.UtcNow.Date.AddDays(1));

        _sectionRepo.Setup(r => r.GetByIdAsync(chapter.Id, default))
            .ReturnsAsync(chapter);

        await _sut.ClearScheduleAsync(chapter.Id, Guid.NewGuid(), default);

        Assert.Null(chapter.ScheduledPublishAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ClearScheduleAsync_WithNonFolder_ThrowsInvariantViolation()
    {
        var section = MakeDocument(Guid.NewGuid(), null);

        _sectionRepo.Setup(r => r.GetByIdAsync(section.Id, default))
            .ReturnsAsync(section);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => _sut.ClearScheduleAsync(section.Id, Guid.NewGuid(), default));
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
