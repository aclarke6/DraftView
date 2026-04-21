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
        var latestVersion = CreateVersion(1, "<p>Content</p>");
        var sectionId = latestVersion.SectionId;

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
        var latestVersion = CreateVersion(3, "<p>Content</p>");
        var sectionId = latestVersion.SectionId;

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
        var section = CreateSection();
        var fromVersion = CreateVersionForSection(section, 1, "<p>Old</p>");
        var latestVersion = CreateVersionForSection(section, 2, "<p>New</p>");
        var sectionId = section.Id;

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
        var section = CreateSection();
        var fromVersion = CreateVersionForSection(section, 5, "<p>Old</p>");
        var latestVersion = CreateVersionForSection(section, 8, "<p>New</p>");
        var sectionId = section.Id;

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
        var section = CreateSection();
        var fromVersion = CreateVersionForSection(section, 1, "<p>From</p>");
        var latestVersion = CreateVersionForSection(section, 2, "<p>To</p>");
        var sectionId = section.Id;

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
    public async Task GetDiffForReaderAsync_DoesNotIntroduceContentBeyondLatestPublishedVersion()
    {
        var section = CreateSection();
        section.UpdateContent("<p>Unpublished working text</p>", "working-hash");

        var fromVersion = CreateVersionForSection(section, 1, "<p>Published v1</p>");
        var latestVersion = CreateVersionForSection(section, 2, "<p>Published v2</p>");

        versionRepo.Setup(r => r.GetLatestAsync(section.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestVersion);
        versionRepo.Setup(r => r.GetAllBySectionIdAsync(section.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SectionVersion> { fromVersion, latestVersion });

        htmlDiffService.Setup(s => s.Compute(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new List<ParagraphDiffResult>());

        await Sut.GetDiffForReaderAsync(section.Id, lastReadVersionNumber: 1);

        htmlDiffService.Verify(s => s.Compute("<p>Published v1</p>", "<p>Published v2</p>"), Times.Once);
        htmlDiffService.Verify(s => s.Compute(It.Is<string>(x => x.Contains("Unpublished")), It.IsAny<string>()), Times.Never);
        htmlDiffService.Verify(s => s.Compute(It.IsAny<string>(), It.Is<string>(x => x.Contains("Unpublished"))), Times.Never);
    }

    [Fact]
    public async Task GetDiffForReaderAsync_WhenFromVersionNotFound_ReturnsHasChangesWithEmptyParagraphs()
    {
        var latestVersion = CreateVersion(5, "<p>Latest</p>");
        var sectionId = latestVersion.SectionId;

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

    private static Section CreateSection()
    {
        var projectId = Guid.NewGuid();
        return Section.CreateDocumentForUpload(projectId, "Test", null, 1);
    }

    private static SectionVersion CreateVersion(int versionNumber, string htmlContent)
    {
        var section = CreateSection();
        return CreateVersionForSection(section, versionNumber, htmlContent);
    }

    private static SectionVersion CreateVersionForSection(Section section, int versionNumber, string htmlContent)
    {
        var authorId = Guid.NewGuid();
        section.UpdateContent(htmlContent, "hash-" + versionNumber);
        return SectionVersion.Create(section, authorId, versionNumber);
    }
}
