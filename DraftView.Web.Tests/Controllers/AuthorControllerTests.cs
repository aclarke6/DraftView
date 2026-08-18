using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Controllers;
using DraftView.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
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
    private readonly Mock<IUserRepository> userRepo = new();
    private readonly Mock<IProjectDiscoveryService> discoveryService = new();
    private readonly Mock<IInvitationRepository> invitationRepo = new();
    private readonly Mock<ISyncProgressTracker> progressTracker = new();
    private readonly Mock<ISyncOrchestrationService> syncOrchestrationService = new();
    private readonly Mock<IReaderAccessRepository> readerAccessRepo = new();
    private readonly Mock<ISectionVersionRepository> sectionVersionRepo = new();
    private readonly Mock<IVersioningService> versioningService = new();
    private readonly Mock<IHtmlDiffService> htmlDiffService = new();
    private readonly Mock<IChangeClassificationService> changeClassificationService = new();
    private readonly Mock<IImportService> importService = new();
    private readonly Mock<ISectionTreeService> sectionTreeService = new();
    private readonly Mock<IUnitOfWork> unitOfWork = new();
    private readonly Mock<IConfiguration> configuration = new();
    private readonly Mock<ILogger<AuthorController>> logger = new();
    private readonly Mock<IAccessRequestService> accessRequestService = new();
    private readonly Mock<IAccessRequestRepository> accessRequestRepo = new();
    private readonly Mock<IUserPreferencesRepository> userPreferencesRepo = new();
    private readonly Mock<UserManager<IdentityUser>> userManager;
    private readonly Mock<SignInManager<IdentityUser>> signInManager;
    private readonly Mock<ISectionManagementService> sectionManagementService = new();
    private readonly Mock<IProjectManagementService> projectManagementService = new();

    public AuthorControllerTests()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
#pragma warning disable CS8625
        userManager = new Mock<UserManager<IdentityUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<IdentityUser>>();
        signInManager = new Mock<SignInManager<IdentityUser>>(
            userManager.Object, contextAccessor.Object, claimsFactory.Object, null, null, null, null);
#pragma warning restore CS8625
    }

    private AuthorController CreateSut(string email = "author@example.test")
    {
        var controller = new AuthorController(
            projectRepo.Object,
            sectionRepo.Object,
            sectionVersionRepo.Object,
            publicationService.Object,
            userService.Object,
            dashboardService.Object,
            userRepo.Object,
            discoveryService.Object,
            invitationRepo.Object,
            progressTracker.Object,
            syncOrchestrationService.Object,
            readerAccessRepo.Object,
            versioningService.Object,
            htmlDiffService.Object,
            changeClassificationService.Object,
            importService.Object,
            sectionTreeService.Object,
            configuration.Object,
            logger.Object,
            accessRequestService.Object,
            accessRequestRepo.Object,
            userPreferencesRepo.Object,
            userManager.Object,
            signInManager.Object,
            sectionManagementService.Object,
            projectManagementService.Object);

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
        configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns((string?)null);

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
    public async Task InviteReader_WhenSystemFailureOccurs_RedirectsToControlledErrorPage()
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

        var result = await sut.InviteReader(new InviteReaderViewModel
        {
            Email = "reader@example.test",
            DisplayName = "Reader Name",
            NeverExpires = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Error", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task InviteReader_WhenDatabaseFailureOccurs_RedirectsToControlledErrorPage()
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
            .ThrowsAsync(new DbUpdateException("Database write failed."));

        var result = await sut.InviteReader(new InviteReaderViewModel
        {
            Email = "reader@example.test",
            DisplayName = "Reader Name",
            NeverExpires = true
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Error", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
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
    public async Task Publishing_WhenProjectExists_ReturnsPublishingPageViewModel()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("Project One", "/Apps/Scrivener/ProjectOne", author.Id, "sync-root");
        var chapter = Section.CreateFolder(project.Id, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(
            project.Id,
            Guid.NewGuid().ToString(),
            "Scene 1",
            chapter.Id,
            0,
            "<p>content</p>",
            "hash",
            null);

        var sut = CreateSut();

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionVersion?)null);

        var result = await sut.Publishing(project.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublishingPageViewModel>(view.Model);
        Assert.Equal(project.Id, model.Project.Id);
        Assert.Single(model.Chapters);
        Assert.Equal(chapter.Id, model.Chapters[0].Chapter.Id);
        Assert.Single(model.Chapters[0].Documents);
    }

    [Fact]
    public async Task Sections_MapsSummaryFromServiceToViewBag()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("Project One", "/Apps/Scrivener/ProjectOne", author.Id, "sync-root");
        var chapter = Section.CreateFolder(project.Id, Guid.NewGuid().ToString(), "Chapter 1", null, 0);

        var summary = new SectionsSummaryDto
        {
            Project = project,
            SortedSections = [new SectionTreeRow(chapter, 0)],
            Publishable = new HashSet<Guid> { chapter.Id },
            ChapterHasChanges = new HashSet<Guid> { chapter.Id },
            ClassificationMap = new Dictionary<Guid, ChangeClassification> { [chapter.Id] = ChangeClassification.Polish }
        };

        var sut = CreateSut();

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        sectionManagementService.Setup(s => s.GetSectionsSummaryAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await sut.Sections(project.Id);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(project, view.ViewData["Project"]);
        var chapterHasChanges = Assert.IsType<HashSet<Guid>>(view.ViewData["ChapterHasChanges"]);
        Assert.Contains(chapter.Id, chapterHasChanges);
        var classificationMap = Assert.IsType<Dictionary<Guid, ChangeClassification>>(view.ViewData["ClassificationMap"]);
        Assert.True(classificationMap.ContainsKey(chapter.Id));
        var model = Assert.IsAssignableFrom<IReadOnlyList<(Section Section, int Depth)>>(view.Model);
        Assert.Single(model);
        Assert.Equal(chapter.Id, model[0].Section.Id);
    }

    [Fact]
    public async Task Sections_WhenProjectNotFound_ReturnsNotFound()
    {
        var project = Project.Create("Project One", "/Apps/Scrivener/ProjectOne", Guid.NewGuid(), "sync-root");
        var sut = CreateSut();

        sectionManagementService.Setup(s => s.GetSectionsSummaryAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SectionsSummaryDto?)null);

        var result = await sut.Sections(project.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Publishing_WhenAtRetentionLimit_ShowsVersionHistoryWithCurrentNotDeletable()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("Project One", "/Apps/Scrivener/ProjectOne", author.Id, "sync-root");
        var chapter = Section.CreateFolder(project.Id, Guid.NewGuid().ToString(), "Chapter 1", null, 0);
        chapter.MarkAsPublishedContainer();
        var document = Section.CreateDocument(
            project.Id,
            Guid.NewGuid().ToString(),
            "Scene 1",
            chapter.Id,
            0,
            "<p>content</p>",
            "hash",
            null);

        var version1 = SectionVersion.Create(document, author.Id, 1, 1, 0);
        var version2 = SectionVersion.Create(document, author.Id, 2, 1, 0);
        var version3 = SectionVersion.Create(document, author.Id, 3, 1, 0);

        var sut = CreateSut();

        configuration.Setup(c => c["DraftView:SubscriptionTier"]).Returns("Free");
        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        sectionRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([chapter, document]);
        sectionVersionRepo.Setup(r => r.GetLatestAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version3);
        sectionVersionRepo.Setup(r => r.GetAllBySectionIdAsync(document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([version1, version2, version3]);

        var result = await sut.Publishing(project.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<PublishingPageViewModel>(view.Model);
        var docModel = Assert.Single(model.Chapters[0].Documents);

        Assert.True(docModel.ShowVersionHistory);
        Assert.Equal(3, docModel.RetentionLimit);
        Assert.Equal(3, docModel.VersionHistory.Count);
        Assert.False(docModel.VersionHistory.First(v => v.VersionNumber == 3).CanDelete);
        Assert.True(docModel.VersionHistory.First(v => v.VersionNumber == 2).CanDelete);
    }

    [Fact]
    public async Task ActivateProject_CallsSetActiveProjectAsync_AndRedirectsToDashboard()
    {
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        var result = await sut.ActivateProject(projectId);

        projectManagementService.Verify(s => s.SetActiveProjectAsync(projectId, It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
    }

    [Fact]
    public async Task ActivateProject_SetsSuccessTempData_WhenServiceSucceeds()
    {
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        await sut.ActivateProject(projectId);

        Assert.Equal("Project activated for readers.", sut.TempData["Success"]);
    }

    [Fact]
    public async Task ActivateProject_SetsErrorTempData_WhenServiceThrows()
    {
        var projectId = Guid.NewGuid();
        projectManagementService
            .Setup(s => s.SetActiveProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Project not found."));
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        await sut.ActivateProject(projectId);

        Assert.Equal("Project not found.", sut.TempData["Error"]);
    }

    [Fact]
    public async Task DeactivateProject_CallsDeactivateProjectAsync_AndRedirectsToDashboard()
    {
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        var result = await sut.DeactivateProject(projectId);

        projectManagementService.Verify(s => s.DeactivateProjectAsync(projectId, It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
    }

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

    [Fact]
    public async Task RepublishDocument_CallsVersioningService_WithCorrectSectionId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sectionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.RepublishDocument(sectionId, projectId);

        versioningService.Verify(v => v.RepublishSectionAsync(sectionId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task RevokeDocument_CallsVersioningService_WithCorrectSectionId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sectionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.RevokeDocument(sectionId, projectId);

        versioningService.Verify(v => v.RevokeLatestVersionAsync(sectionId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task LockChapter_CallsVersioningService_WithCorrectChapterId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.LockChapter(chapterId, projectId);

        versioningService.Verify(v => v.LockChapterAsync(chapterId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task UnlockChapter_CallsVersioningService_WithCorrectChapterId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.UnlockChapter(chapterId, projectId);

        versioningService.Verify(v => v.UnlockChapterAsync(chapterId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task ScheduleChapter_CallsVersioningService_WithCorrectChapterIdAndDate()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scheduledAt = DateTime.Today.AddDays(1);
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.ScheduleChapter(chapterId, projectId, scheduledAt);

        versioningService.Verify(v => v.ScheduleChapterAsync(chapterId, author.Id, scheduledAt.ToUniversalTime(), default), Times.Once);
    }

    [Fact]
    public async Task ClearChapterSchedule_CallsVersioningService_WithCorrectChapterId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var chapterId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.ClearChapterSchedule(chapterId, projectId);

        versioningService.Verify(v => v.ClearScheduleAsync(chapterId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task CreateSection_CallsSectionTreeService_WithCorrectArguments()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var projectId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var created = Section.CreateDocumentForUpload(projectId, "Scene 1", parentId, 1);
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        sectionTreeService.Setup(s => s.CreateSectionAsync(
                projectId,
                "Scene 1",
                NodeType.Document,
                parentId,
                null,
                author.Id,
                default))
            .ReturnsAsync(created);

        await sut.CreateSection(projectId, "Scene 1", NodeType.Document, parentId);

        sectionTreeService.Verify(s => s.CreateSectionAsync(
            projectId,
            "Scene 1",
            NodeType.Document,
            parentId,
            null,
            author.Id,
            default), Times.Once);
    }

    [Fact]
    public async Task MoveSection_ReturnsOk_WhenMoveSucceeds()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sectionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var result = await sut.MoveSection(sectionId, projectId, newParentId, 2);

        Assert.IsType<OkResult>(result);
        sectionTreeService.Verify(s => s.MoveSectionAsync(sectionId, newParentId, 2, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task DeleteSection_CallsSectionTreeService_WithCorrectSectionId()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var sectionId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.DeleteSection(sectionId, projectId);

        sectionTreeService.Verify(s => s.DeleteSectionAsync(sectionId, author.Id, default), Times.Once);
    }

    [Fact]
    public async Task DeleteVersion_CallsVersioningServiceDeleteVersionAsync()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var versionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        await sut.DeleteVersion(versionId, sectionId, Guid.NewGuid());

        versioningService.Verify(v => v.DeleteVersionAsync(versionId, sectionId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Version deleted.", sut.TempData["Success"]);
    }

    [Fact]
    public async Task DeleteVersion_WhenServiceThrowsInvariantViolation_SetsErrorMessage()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var versionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        versioningService
            .Setup(v => v.DeleteVersionAsync(versionId, sectionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvariantViolationException(
                "I-VER-DEL-LATEST", "The current version cannot be deleted. Use Revoke instead."));

        await sut.DeleteVersion(versionId, sectionId, Guid.NewGuid());

        Assert.Equal("The current version cannot be deleted. Use Revoke instead.", sut.TempData["Error"]);
    }

    // -----------------------------------------------------------------------
    // OpenBook / CloseBook
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpenBook_WhenProjectExists_OpensProjectAndSaves()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("My Novel", "/dropbox/test.scriv", author.Id);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var sut = CreateSut();
        var result = await sut.OpenBook(project.Id, "A gripping thriller.");

        Assert.True(project.IsOpen);
        Assert.Equal("A gripping thriller.", project.Brief);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Publishing", redirect.ActionName);
    }

    [Fact]
    public async Task OpenBook_WhenProjectNotFound_ReturnsNotFound()
    {
        projectRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var sut = CreateSut();
        var result = await sut.OpenBook(Guid.NewGuid(), "brief");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CloseBook_WhenProjectExists_ClosesProjectAndBulkDeclines()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("My Novel", "/dropbox/test.scriv", author.Id);
        project.Open("A thriller.");
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var sut = CreateSut();
        var result = await sut.CloseBook(project.Id);

        Assert.False(project.IsOpen);
        accessRequestService.Verify(s => s.BulkDeclineOnRevokeAsync(project.Id, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Publishing", redirect.ActionName);
    }

    [Fact]
    public async Task CloseBook_WhenProjectNotFound_ReturnsNotFound()
    {
        projectRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var sut = CreateSut();
        var result = await sut.CloseBook(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // -----------------------------------------------------------------------
    // BookRequests / ApproveRequest / DeclineRequest
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BookRequests_WhenAuthorOwnsProject_ReturnsViewWithPendingRequests()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var project = Project.Create("My Novel", "/dropbox/test.scriv", author.Id);
        project.Open("A thriller.");
        var readerId = Guid.NewGuid();
        var request = AccessRequest.Create(readerId, project.Id, "I love thrillers", null);
        var reader = User.Create("reader@example.com", "Test Reader", Role.BetaReader);

        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        projectRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        accessRequestRepo.Setup(r => r.GetPendingByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([request]);
        userRepo.Setup(r => r.GetByIdAsync(readerId, It.IsAny<CancellationToken>())).ReturnsAsync(reader);
        userPreferencesRepo.Setup(r => r.GetByUserIdAsync(readerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferences?)null);

        var sut = CreateSut();
        var result = await sut.BookRequests(project.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BookRequestsViewModel>(view.Model);
        Assert.Equal(project.Id, model.Project.Id);
        Assert.Single(model.Requests);
        Assert.Equal("Test Reader", model.Requests[0].ReaderDisplayName);
    }

    [Fact]
    public async Task BookRequests_WhenProjectNotFound_ReturnsNotFound()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        projectRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var sut = CreateSut();
        var result = await sut.BookRequests(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApproveRequest_WhenAuthorOwnsProject_CallsServiceAndRedirects()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var projectId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var sut = CreateSut();
        var result = await sut.ApproveRequest(requestId, projectId);

        accessRequestService.Verify(s => s.ApproveRequestAsync(requestId, author.Id, It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("BookRequests", redirect.ActionName);
    }

    [Fact]
    public async Task DeclineRequest_WhenAuthorOwnsProject_CallsServiceAndRedirects()
    {
        var author = User.Create("author@example.test", "Author", Role.Author);
        var projectId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        userRepo.Setup(r => r.GetByEmailAsync("author@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var sut = CreateSut();
        var result = await sut.DeclineRequest(requestId, projectId);

        accessRequestService.Verify(s => s.DeclineRequestAsync(requestId, author.Id, It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("BookRequests", redirect.ActionName);
    }

    // -----------------------------------------------------------------------
    // ImpersonateReader guards
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ImpersonateReader_WhenReaderNotFound_ReturnsNotFound()
    {
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        var result = await sut.ImpersonateReader(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ImpersonateReader_WhenUserIsNotBetaReader_RedirectsToReadersWithError()
    {
        var authorUser = User.Create("other@example.test", "Other Author", Role.Author);
        userRepo.Setup(r => r.GetByIdAsync(authorUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorUser);

        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        var result = await sut.ImpersonateReader(authorUser.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Readers", redirect.ActionName);
        Assert.NotNull(sut.TempData["Error"]);
    }

    [Fact]
    public async Task ImpersonateReader_WhenReaderHasNoIdentityAccount_RedirectsToReadersWithError()
    {
        var reader = User.Create("reader@example.test", "Test Reader", Role.BetaReader);
        userRepo.Setup(r => r.GetByIdAsync(reader.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        userManager.Setup(m => m.FindByEmailAsync("reader@example.test"))
            .ReturnsAsync((IdentityUser?)null);

        var sut = CreateSut();
        sut.TempData = new TempDataDictionary(sut.HttpContext, Mock.Of<ITempDataProvider>());

        var result = await sut.ImpersonateReader(reader.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Readers", redirect.ActionName);
        Assert.NotNull(sut.TempData["Error"]);
    }
}
