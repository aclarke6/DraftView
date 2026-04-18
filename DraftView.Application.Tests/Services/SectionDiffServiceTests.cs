using DraftView.Application.Services;
using DraftView.Domain.Contracts;
using DraftView.Domain.Diff;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Moq;
using Xunit;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for SectionDiffService.GetDiffForReaderAsync.
/// Covers: version lookup, reader state handling, diff computation coordination.
/// Excludes: HTML diff logic (covered in HtmlDiffServiceTests), UI rendering (Web layer).
/// </summary>
public class SectionDiffServiceTests
{
    private readonly Mock<ISectionVersionRepository> versionRepo = new();
    private readonly Mock<IHtmlDiffService> htmlDiffService = new();
    private SectionDiffService Sut => new(versionRepo.Object, htmlDiffService.Object);

    [Fact]
    public async Task GetDiffForReaderAsync_WhenNoVersionExists_ReturnsNull()
    {
        var sectionId = Guid.NewGuid();

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionVersion?)null);

        var result = await Sut.GetDiffForReaderAsync(sectionId, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenReaderHasNeverRead_ReturnsNoChanges()
    {
        var sectionId = Guid.NewGuid();
        var latestVersion = CreateVersion(sectionId, 1, "<p>Content</p>");

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);

        var result = await Sut.GetDiffForReaderAsync(sectionId, lastReadVersionNumber: null);

        Assert.NotNull(result);
        Assert.False(result.HasChanges);
        Assert.Null(result.FromVersionNumber);
        Assert.Equal(1, result.CurrentVersionNumber);
        Assert.Empty(result.Paragraphs);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenReaderIsOnLatestVersion_ReturnsNoChanges()
    {
        var sectionId = Guid.NewGuid();
        var latestVersion = CreateVersion(sectionId, 3, "<p>Content</p>");

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);

        var result = await Sut.GetDiffForReaderAsync(sectionId, lastReadVersionNumber: 3);

        Assert.NotNull(result);
        Assert.False(result.HasChanges);
        Assert.Equal(3, result.FromVersionNumber);
        Assert.Equal(3, result.CurrentVersionNumber);
        Assert.Empty(result.Paragraphs);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenNewerVersionExists_ReturnsHasChanges()
    {
        var sectionId = Guid.NewGuid();
        var fromVersion = CreateVersion(sectionId, 1, "<p>Old</p>");
        var latestVersion = CreateVersion(sectionId, 2, "<p>New</p>");

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);
        versionRepo.Setup(r => r.GetAllBySectionIdAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectionVersion> { fromVersion, latestVersion });

        htmlDiffService.Setup(s => s.Compute("<p>Old</p>", "<p>New</p>"))
            .Returns(new List<ParagraphDiffResult>
            {
                new("Old", "<p>Old</p>", DiffResultType.Removed),
                new("New", "<p>New</p>", DiffResultType.Added)
            });

        var result = await Sut.GetDiffForReaderAsync(sectionId, lastReadVersionNumber: 1);

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
        Assert.Equal(1, result.FromVersionNumber);
        Assert.Equal(2, result.CurrentVersionNumber);
        Assert.Equal(2, result.Paragraphs.Count);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenNewerVersionExists_ReturnsCorrectVersionNumbers()
    {
        var sectionId = Guid.NewGuid();
        var fromVersion = CreateVersion(sectionId, 5, "<p>Old</p>");
        var latestVersion = CreateVersion(sectionId, 8, "<p>New</p>");

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);
        versionRepo.Setup(r => r.GetAllBySectionIdAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectionVersion> { fromVersion, latestVersion });

        htmlDiffService.Setup(s => s.Compute(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<ParagraphDiffResult>());

        var result = await Sut.GetDiffForReaderAsync(sectionId, lastReadVersionNumber: 5);

        Assert.NotNull(result);
        Assert.Equal(5, result.FromVersionNumber);
        Assert.Equal(8, result.CurrentVersionNumber);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenNewerVersionExists_CallsDiffService()
    {
        var sectionId = Guid.NewGuid();
        var fromVersion = CreateVersion(sectionId, 1, "<p>From</p>");
        var latestVersion = CreateVersion(sectionId, 2, "<p>To</p>");

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);
        versionRepo.Setup(r => r.GetAllBySectionIdAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectionVersion> { fromVersion, latestVersion });

        htmlDiffService.Setup(s => s.Compute(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<ParagraphDiffResult>());

        await Sut.GetDiffForReaderAsync(sectionId, lastReadVersionNumber: 1);

        htmlDiffService.Verify(s => s.Compute("<p>From</p>", "<p>To</p>"), Times.Once);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenFromVersionNotFound_ReturnsHasChangesWithEmptyParagraphs()
    {
        var sectionId = Guid.NewGuid();
        var latestVersion = CreateVersion(sectionId, 5, "<p>Latest</p>");

        versionRepo.Setup(r => r.GetLatestAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);
        versionRepo.Setup(r => r.GetAllBySectionIdAsync(sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectionVersion> { latestVersion });

        var result = await Sut.GetDiffForReaderAsync(sectionId, lastReadVersionNumber: 2);

        Assert.NotNull(result);
        Assert.True(result.HasChanges);
        Assert.Equal(2, result.FromVersionNumber);
        Assert.Equal(5, result.CurrentVersionNumber);
        Assert.Empty(result.Paragraphs);
    }

    private static SectionVersion CreateVersion(Guid sectionId, int versionNumber, string htmlContent)
    {
        var authorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var section = Section.CreateDocumentForUpload(projectId, "Test", null, 1);

        // Use reflection to set the Id and HtmlContent since Section uses private setters
        var idProperty = typeof(Section).GetProperty("Id");
        idProperty?.SetValue(section, sectionId);

        var htmlContentProperty = typeof(Section).GetProperty("HtmlContent");
        htmlContentProperty?.SetValue(section, htmlContent);

        var contentHashProperty = typeof(Section).GetProperty("ContentHash");
        contentHashProperty?.SetValue(section, "hash-" + versionNumber);

        return SectionVersion.Create(section, authorId, versionNumber);
    }
}
