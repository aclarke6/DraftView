using System.Net;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Controllers;
using DraftView.Web.Services;
using DraftView.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace DraftView.Web.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Privacy_ReturnsView()
    {
        var userRepo = new Mock<IUserRepository>();
        var controller = new HomeController(userRepo.Object);

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }
}

public sealed class HomeErrorPagesIntegrationTests : IClassFixture<HomeErrorPagesIntegrationTests.HomeErrorPagesWebFactory>
{
    private readonly HomeErrorPagesWebFactory _factory;

    public HomeErrorPagesIntegrationTests(HomeErrorPagesWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Test403_WithTrueForbiddenStatus_RendersForbiddenErrorPageWithExpectedData()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/Home/Test403");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("You do not have permission to access this page.", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Source Area", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Web", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test404_WithTrueNotFoundStatus_RendersNotFoundPage()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/Home/Test404");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The trail you’ve chosen slips beyond the borders of any realm we can enter.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test405_WithTrueMethodNotAllowedStatus_RendersMethodNotAllowedPageWithExpectedData()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/Home/Test405");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("Method not allowed", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("This endpoint does not allow the attempted HTTP method.", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DraftView", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test500_WithTrueInternalServerErrorStatus_RendersErrorPageWithExpectedData()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/Home/Test500");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Something went wrong", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The system could not complete this request.", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reference", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Request Path", html, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class HomeErrorPagesWebFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=draftview_tests;Username=test;Password=test",
                    ["EmailProtection:EncryptionKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                    ["EmailProtection:LookupHmacKey"] = "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=",
                    ["Email:Provider"] = "Console"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();

                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IUserPreferencesRepository>();
                services.RemoveAll<ISystemStateMessageService>();

                services.AddSingleton(Mock.Of<IUserRepository>());
                services.AddSingleton(Mock.Of<IUserPreferencesRepository>());
                services.AddSingleton(Mock.Of<ISystemStateMessageService>());
            });
        }
    }
}
