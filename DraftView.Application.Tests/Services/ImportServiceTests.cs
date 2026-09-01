using System.Security.Cryptography;
using System.Text;
using DraftView.Application.Services;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using Moq;

namespace DraftView.Application.Tests.Services;

public class ImportServiceTests
{
    private readonly Mock<ISectionRepository> sectionRepository = new();
    private readonly Mock<IUnitOfWork>        unitOfWork        = new();
    private readonly Mock<IImportProvider>    importProvider    = new();

    private ImportService CreateSut(params IImportProvider[] providers) => new(
        sectionRepository.Object,
        unitOfWork.Object,
        providers.Length == 0 ? new[] { importProvider.Object } : providers);

    private static Section CreateSection(Guid projectId) =>
        Section.CreateDocumentForUpload(projectId, "Scene 1", null, 1);

    [Fact]
    public async Task ImportAsync_WritesHtmlToSection()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await CreateSut().ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.Equal("<p>Hello</p>", section.HtmlContent);
    }

    [Fact]
    public async Task ImportAsync_UpdatesContentHash()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await CreateSut().ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("<p>Hello</p>"))), section.ContentHash);
    }

    [Fact]
    public async Task ImportAsync_AlwaysMarksDirtyFlag()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");
        unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await CreateSut().ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid());

        Assert.True(section.ContentChangedSincePublish);
    }

    [Fact]
    public async Task ImportAsync_Throws_ForUnsupportedExtension()
    {
        var projectId = Guid.NewGuid();
        var section = CreateSection(projectId);
        sectionRepository.Setup(r => r.GetByIdAsync(section.Id, default)).ReturnsAsync(section);

        var ex = await Assert.ThrowsAsync<UnsupportedFileTypeException>(() =>
            CreateSut().ImportAsync(projectId, section.Id, new MemoryStream(Encoding.UTF8.GetBytes("plain")), "scene.txt", Guid.NewGuid()));

        Assert.Equal(".txt", ex.Extension);
    }

    [Fact]
    public async Task ImportAsync_Throws_WhenSectionNotFound()
    {
        var projectId = Guid.NewGuid();
        sectionRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Section?)null);
        importProvider.SetupGet(p => p.SupportedExtension).Returns(".rtf");
        importProvider.Setup(p => p.ConvertToHtmlAsync(It.IsAny<Stream>(), default)).ReturnsAsync("<p>Hello</p>");

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            CreateSut().ImportAsync(projectId, Guid.NewGuid(), new MemoryStream(Encoding.UTF8.GetBytes("{\\rtf1 hello}")), "scene.rtf", Guid.NewGuid()));
    }
}
