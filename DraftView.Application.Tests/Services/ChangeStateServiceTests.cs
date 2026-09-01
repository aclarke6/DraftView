using Moq;
using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Tests.Services;

public class ChangeStateServiceTests
{
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();
    private const string OldHtml    = "<p>Old content here.</p>";
    private const string CurrentHtml = "<p>Current content here with changes.</p>";

    private readonly Mock<ISectionRepository>         _sectionRepo          = new();
    private readonly Mock<IReaderSnapshotRepository>  _snapshotRepo         = new();
    private readonly Mock<IHtmlDiffService>           _htmlDiffService      = new();
    private readonly Mock<IChangeClassificationService> _classificationService = new();

    private ChangeStateService CreateSut() => new(
        _sectionRepo.Object,
        _snapshotRepo.Object,
        _htmlDiffService.Object,
        _classificationService.Object);

    private static Section MakeSection(string? htmlContent)
    {
        var section = (Section)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Section));
        typeof(Section).GetProperty("HtmlContent")!
            .SetValue(section, htmlContent);
        return section;
    }

    // ---------------------------------------------------------------------------
    // No snapshot → New
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetChangeStateAsync_NoSnapshot_ReturnsNew()
    {
        _sectionRepo.Setup(r => r.GetByIdAsync(SectionId, default))
            .ReturnsAsync(MakeSection(CurrentHtml));
        _snapshotRepo.Setup(r => r.GetAsync(SectionId, UserId, default))
            .ReturnsAsync((ReaderSnapshot?)null);

        var result = await CreateSut().GetChangeStateAsync(SectionId, UserId);

        Assert.Equal(ChangeClassification.New, result);
    }

    [Fact]
    public async Task GetChangeStateAsync_NullSection_ReturnsNew()
    {
        _sectionRepo.Setup(r => r.GetByIdAsync(SectionId, default))
            .ReturnsAsync((Section?)null);

        var result = await CreateSut().GetChangeStateAsync(SectionId, UserId);

        Assert.Equal(ChangeClassification.New, result);
    }

    // ---------------------------------------------------------------------------
    // Snapshot matches current content → null (no change)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetChangeStateAsync_SnapshotMatchesCurrent_ReturnsNull()
    {
        _sectionRepo.Setup(r => r.GetByIdAsync(SectionId, default))
            .ReturnsAsync(MakeSection(CurrentHtml));
        var snapshot = ReaderSnapshot.Create(SectionId, UserId, CurrentHtml);
        _snapshotRepo.Setup(r => r.GetAsync(SectionId, UserId, default))
            .ReturnsAsync(snapshot);

        var result = await CreateSut().GetChangeStateAsync(SectionId, UserId);

        Assert.Null(result);
        _htmlDiffService.Verify(s => s.Compute(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Snapshot differs → word diff → classification
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetChangeStateAsync_SnapshotDiffers_ReturnsDiffClassification()
    {
        _sectionRepo.Setup(r => r.GetByIdAsync(SectionId, default))
            .ReturnsAsync(MakeSection(CurrentHtml));
        var snapshot = ReaderSnapshot.Create(SectionId, UserId, OldHtml);
        _snapshotRepo.Setup(r => r.GetAsync(SectionId, UserId, default))
            .ReturnsAsync(snapshot);

        var diffResult = new[] { new ParagraphDiffResult(
            "old", CurrentHtml, DiffResultType.Modified,
            wordsAdded: 20, wordsRemoved: 5, totalWords: 100) };
        _htmlDiffService.Setup(s => s.Compute(OldHtml, CurrentHtml)).Returns(diffResult);
        _classificationService.Setup(s => s.Classify(diffResult))
            .Returns(ChangeClassification.Polish);

        var result = await CreateSut().GetChangeStateAsync(SectionId, UserId);

        Assert.Equal(ChangeClassification.Polish, result);
    }

    [Fact]
    public async Task GetChangeStateAsync_SnapshotDiffers_DiffReturnsNull_ReturnsNull()
    {
        _sectionRepo.Setup(r => r.GetByIdAsync(SectionId, default))
            .ReturnsAsync(MakeSection(CurrentHtml));
        var snapshot = ReaderSnapshot.Create(SectionId, UserId, OldHtml);
        _snapshotRepo.Setup(r => r.GetAsync(SectionId, UserId, default))
            .ReturnsAsync(snapshot);

        _htmlDiffService.Setup(s => s.Compute(OldHtml, CurrentHtml))
            .Returns(Array.Empty<ParagraphDiffResult>());
        _classificationService.Setup(s => s.Classify(It.IsAny<ParagraphDiffResult[]>()))
            .Returns((ChangeClassification?)null);

        var result = await CreateSut().GetChangeStateAsync(SectionId, UserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetChangeStateAsync_SnapshotDiffers_PassesSnapshotHtmlAsFrom()
    {
        _sectionRepo.Setup(r => r.GetByIdAsync(SectionId, default))
            .ReturnsAsync(MakeSection(CurrentHtml));
        var snapshot = ReaderSnapshot.Create(SectionId, UserId, OldHtml);
        _snapshotRepo.Setup(r => r.GetAsync(SectionId, UserId, default))
            .ReturnsAsync(snapshot);

        _htmlDiffService.Setup(s => s.Compute(OldHtml, CurrentHtml))
            .Returns(Array.Empty<ParagraphDiffResult>());
        _classificationService.Setup(s => s.Classify(It.IsAny<ParagraphDiffResult[]>()))
            .Returns((ChangeClassification?)null);

        await CreateSut().GetChangeStateAsync(SectionId, UserId);

        _htmlDiffService.Verify(s => s.Compute(OldHtml, CurrentHtml), Times.Once);
    }
}
