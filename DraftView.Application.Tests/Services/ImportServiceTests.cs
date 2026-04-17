using System.Security.Cryptography;
using System.Text;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Moq;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ImportService covering provider resolution and section content updates.
/// Excludes SectionVersion creation, which is not allowed in this phase.
/// </summary>
public class ImportServiceTests
{
    private readonly Mock<ISectionRepository> sectionRepository = new();
    private readonly Mock<ISectionVersionRepository> sectionVersionRepository = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly Mock<IImportProvider> importProvider = new();

    private ImportService CreateSut(params IImportProvider[] providers) => new(
        sectionRepository.Object,
        sectionVersionRepository.Object,
        unitOfWork.Object,
        providers.Length == 0 ? new[] { importProvider.Object } : providers);

    private static Section CreateSection(Guid projectId) =>
        Section.CreateDocumentForUpload(projectId, "Scene 1", null, 1);

    private static SectionVersion CreateSectionVersion()
    {
        var section = Section.CreateDocumentForUpload(Guid.NewGuid(), "Scene 1", null, 1);
        section.UpdateContent("<p>Published</p>", "hash");
        return SectionVersion.Create(section, Guid.NewGuid(), 1);
    }

    /// <summary>Import should write converted HTML into the section.</summary>
    [Fact]
    public async Task ImportAsync_WritesHtmlToSection()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        sectionVersionRepository.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync((SectionVersion?)null);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.Equal("<p>Hello</p>", section.HtmlContent);
    }

    /// <summary>Import should update the content hash.</summary>
    [Fact]
    public async Task ImportAsync_UpdatesContentHash()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        sectionVersionRepository.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync((SectionVersion?)null);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("<p>Hello</p>"))), section.ContentHash);
    }

    /// <summary>Import should mark content changed when a version exists.</summary>
    [Fact]
    public async Task ImportAsync_SetsDirtyFlag_WhenVersionExists()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        sectionVersionRepository.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync(CreateSectionVersion());
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.True(section.ContentChangedSincePublish);
    }

    /// <summary>Import should not mark content changed when no version exists.</summary>
    [Fact]
    public async Task ImportAsync_DoesNotSetDirtyFlag_WhenNoVersionExists()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        sectionVersionRepository.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync((SectionVersion?)null);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.False(section.ContentChangedSincePublish);
    }

    /// <summary>Unsupported extensions should fail before any section write.</summary>
    [Fact]
    public async Task ImportAsync_Throws_ForUnsupportedExtension()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<UnsupportedFileTypeException>(() =>
            sut.ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("plain")), "scene.txt", Guid.NewGuid()));

        Assert.Equal(".txt", ex.Extension);
    }

    /// <summary>Missing sections should throw EntityNotFoundException.</summary>
    [Fact]
    public async Task ImportAsync_Throws_WhenSectionNotFound()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Section?)null);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        var sut = CreateSut();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            sut.ImportAsync(projectId, Guid.NewGuid(), new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid()));
    }

    /// <summary>Import must never create section versions.</summary>
    [Fact]
    public async Task ImportAsync_NeverCreatesVersion()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        sectionVersionRepository.Setup(r => r.GetLatestAsync(section.Id, default)).ReturnsAsync((SectionVersion?)null);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        sectionVersionRepository.Verify(r => r.AddAsync(It.IsAny<SectionVersion>(), default), Times.Never);
    }
}
