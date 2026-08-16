using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Controllers;
using DraftView.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using System.Security.Claims;

namespace DraftView.Web.Tests.Controllers;

public class DiscoveryControllerTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid ReaderId = Guid.NewGuid();

    private readonly Mock<IProjectRepository> projectRepo = new();
    private readonly Mock<IAccessRequestRepository> accessRequestRepo = new();
    private readonly Mock<IAccessRequestService> accessRequestService = new();
    private readonly Mock<IUserRepository> userRepo = new();
    private readonly Mock<ITempDataDictionary> tempData = new();

    private DiscoveryController CreateSut(bool authenticated = false, Guid? userId = null, string role = "BetaReader")
    {
        var controller = new DiscoveryController(
            projectRepo.Object,
            accessRequestRepo.Object,
            accessRequestService.Object,
            userRepo.Object);

        var claims = new List<Claim>();
        if (authenticated)
        {
            claims.Add(new Claim(ClaimTypes.Name, "reader@example.com"));
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        var identity = authenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<Microsoft.AspNetCore.Routing.LinkGenerator>());
        controller.ControllerContext.HttpContext.RequestServices = services.BuildServiceProvider();
        var urlHelper = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        urlHelper.Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>())).Returns("/Discovery/Index");
        controller.Url = urlHelper.Object;
        controller.TempData = tempData.Object;
        return controller;
    }

    private static Project MakeOpenProject(string name = "My Novel")
    {
        var p = Project.Create(name, "/dropbox/test.scriv", AuthorId);
        p.Open("A gripping thriller.");
        return p;
    }

    // -----------------------------------------------------------------------
    // Index
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Index_WhenNoOpenProjects_ReturnsEmptyList()
    {
        projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateSut();
        var result = await sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DiscoveryViewModel>(view.Model);
        Assert.Empty(model.Projects);
    }

    [Fact]
    public async Task Index_ReturnsOnlyOpenProjects()
    {
        var open = MakeOpenProject();
        var closed = Project.Create("Closed Book", "/dropbox/closed.scriv", AuthorId);

        projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([open, closed]);

        var sut = CreateSut();
        var result = await sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DiscoveryViewModel>(view.Model);
        Assert.Single(model.Projects);
        Assert.Equal(open.Id, model.Projects[0].Project.Id);
    }

    [Fact]
    public async Task Index_WhenAuthenticatedReader_IncludesRequestStatus()
    {
        var open = MakeOpenProject();
        var reader = User.Create("reader@example.com", "Test Reader", Role.BetaReader);
        var request = AccessRequest.Create(reader.Id, open.Id, null, null);

        projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([open]);
        userRepo.Setup(r => r.GetByEmailAsync("reader@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        accessRequestRepo.Setup(r => r.GetVisibleByReaderIdAsync(reader.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([request]);

        var sut = CreateSut(authenticated: true);
        var result = await sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DiscoveryViewModel>(view.Model);
        Assert.True(model.IsAuthenticated);
        Assert.Equal(DiscoveryRequestStatus.Pending, model.Projects[0].RequestStatus);
    }

    [Fact]
    public async Task Index_WhenAnonymous_IsAuthenticatedFalse()
    {
        projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateSut(authenticated: false);
        var result = await sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DiscoveryViewModel>(view.Model);
        Assert.False(model.IsAuthenticated);
    }

    // -----------------------------------------------------------------------
    // SubmitRequest
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SubmitRequest_WhenSuccess_RedirectsToIndexWithSuccess()
    {
        var reader = User.Create("reader@example.com", "Test Reader", Role.BetaReader);
        var projectId = Guid.NewGuid();

        userRepo.Setup(r => r.GetByEmailAsync("reader@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);

        var sut = CreateSut(authenticated: true);
        var result = await sut.SubmitRequest(projectId, "Love thrillers", null);

        accessRequestService.Verify(s => s.SubmitRequestAsync(reader.Id, projectId, "Love thrillers", null, It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task SubmitRequest_WhenDuplicateRequest_RedirectsWithError()
    {
        var reader = User.Create("reader@example.com", "Test Reader", Role.BetaReader);
        var projectId = Guid.NewGuid();

        userRepo.Setup(r => r.GetByEmailAsync("reader@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reader);
        accessRequestService.Setup(s => s.SubmitRequestAsync(reader.Id, projectId, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvariantViolationException("I-AR-DUP", "Duplicate"));

        var sut = CreateSut(authenticated: true);
        var result = await sut.SubmitRequest(projectId, null, null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        tempData.VerifySet(t => t["Error"] = It.IsAny<object>(), Times.Once);
    }
}
