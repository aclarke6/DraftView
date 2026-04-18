using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Controllers;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DraftView.Web.Tests.Controllers;

public class AuthorControllerTests
{
    private readonly Mock<IProjectRepository> projectRepo = new();
    private readonly Mock<ISectionRepository> sectionRepo = new();
    private readonly Mock<IPublicationService> publicationService = new();
    private readonly Mock<IUserService> userService = new();
    private readonly Mock<IDashboardService> dashboardService = new();
    private readonly Mock<ISyncService> syncService = new();
    private readonly Mock<IUserRepository> userRepo = new();
    private readonly Mock<IProjectDiscoveryService> discoveryService = new();
    private readonly Mock<IInvitationRepository> invitationRepo = new();
    private readonly Mock<IServiceScopeFactory> scopeFactory = new();
    private readonly Mock<ISyncProgressTracker> progressTracker = new();
    private readonly Mock<IReaderAccessRepository> readerAccessRepo = new();
    private readonly Mock<ISectionVersionRepository> sectionVersionRepo = new();
    private readonly Mock<IVersioningService> versioningService = new();
    private readonly Mock<IHtmlDiffService> htmlDiffService = new();
    private readonly Mock<IChangeClassificationService> changeClassificationService = new();
    private readonly Mock<IImportService> importService = new();
    private readonly Mock<ISectionTreeService> sectionTreeService = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly Mock<ILogger<AuthorController>> logger = new();

    private AuthorController CreateSut(string email = "author@example.test")
    {
        var controller = new AuthorController(
            projectRepo.Object,
            sectionRepo.Object,
            sectionVersionRepo.Object,
            publicationService.Object,
            userService.Object,
            dashboardService.Object,
            syncService.Object,
            userRepo.Object,
            discoveryService.Object,
            invitationRepo.Object,
            scopeFactory.Object,
            progressTracker.Object,
            readerAccessRepo.Object,
            versioningService.Object,
            htmlDiffService.Object,
            changeClassificationService.Object,
            importService.Object,
            sectionTreeService.Object,
            logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, email)],
                        "TestAuth"))
            }
        };
        var services = new ServiceCollection();
        services.AddSingleton(unitOfWork.Object);
        controller.ControllerContext.HttpContext.RequestServices = services.BuildServiceProvider();
        var urlHelper = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string?>())).Returns(false);
        controller.Url = urlHelper.Object;
        controller.TempData = new Mock<ITempDataDictionary>().Object;

        return controller;
    }

    [Fact]
    public async Task InviteReader_WhenValidationFails_ReturnsViewWithFriendlyMessage()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sut = CreateSut();

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        userService.Setup(s => s.IssueInvitationAsync(
                "reader@example.test",
                "reader@example.test",
                ExpiryPolicy.AlwaysOpen,
                null,
                author.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvariantViolationException("I-DISPLAYNAME", "Display name must not be an email address. Please enter a real name or label."));

        var result = await sut.InviteReader(new InviteReaderViewModel
        {
            Email = "reader@example.test",
            DisplayName = "reader@example.test",
            NeverExpires = true
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<InviteReaderViewModel>(view.Model);
        Assert.False(sut.ModelState.IsValid);
    }

    [Fact]
    public async Task InviteReader_WhenSystemFailureOccurs_BubblesException()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sut = CreateSut();

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        userService.Setup(s => s.IssueInvitationAsync(
                "reader@example.test",
                "Reader Name",
                ExpiryPolicy.AlwaysOpen,
                null,
                author.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Missing required configuration value 'App:BaseUrl'."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InviteReader(new InviteReaderViewModel
        {
            Email = "reader@example.test",
            DisplayName = "Reader Name",
            NeverExpires = true
        }));
    }

    [Fact]
    public async Task SoftDeleteReader_WhenAuthorRemovesReader_SoftDeletesUser()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("Project One", "/Apps/Scrivener/ProjectOne", author.Id, "sync-root");
        var sut = CreateSut();
        var readerId = Guid.NewGuid();

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([project]);
        readerAccessRepo.Setup(r => r.GetByReaderAndProjectAsync(readerId, project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReaderAccess?)null);

        var result = await sut.SoftDeleteReader(readerId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Readers", redirect.ActionName);
        userService.Verify(s => s.SoftDeleteUserAsync(readerId, author.Id, It.IsAny<CancellationToken>()), Times.Once);
        userService.Verify(s => s.DeactivateUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dashboard_WhenCurrentUserIsNotAuthor_RedirectsToReaderIndex()
    {
        var reader = User.Create("author@example.test", "Reader", Role.BetaReader);
        var sut = CreateSut();

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);

        var result = await sut.Dashboard();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Reader", redirect.ControllerName);
    }

    // ---------------------------------------------------------------------------
    // RepublishChapter
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RepublishChapter_CallsVersioningService_WithCorrectChapterId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.RepublishChapter(chapterId, projectId);

        versioningService.Verify(v => v.RepublishChapterAsync(chapterId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task RepublishChapter_SetsTempDataSuccess_WhenRepublishSucceeds()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.RepublishChapter(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal("Chapter republished. Readers will see the updated content.", sut.TempData["Success"]);
    }

    [Fact]
    public async Task RepublishChapter_SetsTempDataError_WhenVersioningServiceThrows()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        versioningService.Setup(v => v.RepublishChapterAsync(chapterId, author.Id, default))
            .ThrowsAsync(new InvariantViolationException("I-VER-NO-DOCS", "No documents"));

        await sut.RepublishChapter(chapterId, Guid.NewGuid());

        Assert.Equal("No documents", sut.TempData["Error"]);
    }

    [Fact]
    public async Task RepublishChapter_RedirectsToSections_AfterRepublish()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var result = await sut.RepublishChapter(chapterId, projectId);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains($"#section-{chapterId}", redirect.Url);
    }
}
