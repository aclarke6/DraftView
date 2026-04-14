using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;
using DraftView.Infrastructure.Persistence;
using DraftView.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace DraftView.Web.Tests.EmailExposure;

public class GoverningRenderedEmailExposureTests :
    IClassFixture<GoverningRenderedEmailExposureTests.RenderedPrivacyFactory>
{
    private const string KnownEmail = "governing.leak@example.test";
    private readonly RenderedPrivacyFactory factory;

    public GoverningRenderedEmailExposureTests(RenderedPrivacyFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// LONG-RUNNING GOVERNING REGRESSION TEST.
    ///
    /// Enforces that non-whitelisted pages must not display a known stored
    /// email address value in final rendered HTML.
    ///
    /// This test MUST remain RED until Phase 2 (Web Surface Cleanup) is implemented.
    ///
    /// Failure indicates a GDPR-critical rendered-output exposure risk, including
    /// layout-driven leaks that source scanning alone cannot prove.
    /// </summary>
    [Theory]
    [InlineData("/Author/Readers", true)]
    [InlineData("/Author/Dashboard", true)]
    [InlineData("/Account/AcceptInvitation", false)]
    public async Task Governing_RenderedOutput_NoStoredEmailDisplayedInNonWhitelistedPages_MUST_FAIL_UNTIL_PHASE2(
        string path,
        bool authenticateAsAuthor)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        if (authenticateAsAuthor)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, TestAuthHandler.AuthorMode);
        }

        var requestPath = path == "/Account/AcceptInvitation"
            ? $"{path}?token={factory.Invitation.Token}"
            : path;

        var response = await client.GetAsync(requestPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(KnownEmail, html, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class RenderedPrivacyFactory : WebApplicationFactory<Program>
    {
        private const string DatabaseName = "draftview_tests";
        private readonly User authorUser = User.Create(KnownEmail, "Governing Author", Role.Author);
        private readonly User invitedReader = User.Create(KnownEmail, "Pending", Role.BetaReader);
        private readonly UserPreferences authorPrefs;

        public Invitation Invitation { get; }

        public RenderedPrivacyFactory()
        {
            authorUser.Activate();
            authorPrefs = UserPreferences.CreateForAuthor(
                authorUser.Id, AuthorDigestMode.Immediate, null, "Europe/London");
            Invitation = Invitation.CreateAlwaysOpen(invitedReader.Id);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                var baseConnectionString = LoadBaseConnectionString();
                var testConnectionString = BuildTestConnectionString(baseConnectionString, DatabaseName);

                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = testConnectionString,
                    ["Seed:AuthorEmail"] = KnownEmail,
                    ["Seed:AuthorPassword"] = "Password1!",
                    ["Seed:AuthorName"] = "Governing Author",
                    ["Seed:SupportEmail"] = "support@example.test",
                    ["Seed:SupportPassword"] = "Password1!",
                    ["Seed:SupportName"] = "Support",
                    ["Seed:TestProjectPath"] = "/Apps/Scrivener/Test.scriv",
                    ["Email:Provider"] = "Console"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();

                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });

                services.PostConfigureAll<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });

                var userRepo = new Mock<IUserRepository>();
                userRepo.Setup(r => r.GetByIdAsync(TestAuthHandler.AuthorId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(authorUser);
                userRepo.Setup(r => r.GetByIdAsync(authorUser.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(authorUser);
                userRepo.Setup(r => r.GetByIdAsync(invitedReader.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(invitedReader);
                userRepo.Setup(r => r.GetByEmailAsync(KnownEmail, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(authorUser);
                userRepo.Setup(r => r.GetAuthorAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(authorUser);
                userRepo.Setup(r => r.GetAllBetaReadersAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync([invitedReader]);
                userRepo.Setup(r => r.CountActiveBetaReadersAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(0);

                var invitationRepo = new Mock<IInvitationRepository>();
                invitationRepo.Setup(r => r.GetByTokenAsync(Invitation.Token, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Invitation);
                invitationRepo.Setup(r => r.GetByUserIdAsync(invitedReader.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Invitation);

                var prefsRepo = new Mock<IUserPreferencesRepository>();
                prefsRepo.Setup(r => r.GetByUserIdAsync(authorUser.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(authorPrefs);

                var dashboardService = new Mock<IDashboardService>();
                dashboardService.Setup(s => s.GetEmailHealthSummaryAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<EmailDeliveryLog>());
                dashboardService.Setup(s => s.GetNotificationsAsync(authorUser.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<AuthorNotification>());

                var publicationService = new Mock<IPublicationService>();
                publicationService.Setup(s => s.GetPublishedChaptersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<Section>());

                var projectRepo = new Mock<IScrivenerProjectRepository>();
                projectRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Array.Empty<ScrivenerProject>());
                projectRepo.Setup(r => r.GetReaderActiveProjectAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ScrivenerProject?)null);

                var systemStateMessageService = new Mock<ISystemStateMessageService>();
                systemStateMessageService.Setup(s => s.GetActiveMessageAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync((SystemStateMessage?)null);

                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IInvitationRepository>();
                services.RemoveAll<IUserPreferencesRepository>();
                services.RemoveAll<IDashboardService>();
                services.RemoveAll<IPublicationService>();
                services.RemoveAll<IScrivenerProjectRepository>();
                services.RemoveAll<ISystemStateMessageService>();
                services.RemoveAll<ISectionRepository>();
                services.RemoveAll<IUserService>();
                services.RemoveAll<ISyncService>();
                services.RemoveAll<IScrivenerProjectDiscoveryService>();
                services.RemoveAll<IReaderAccessRepository>();
                services.RemoveAll<ISyncProgressTracker>();
                services.RemoveAll<IEmailSender>();

                services.AddSingleton(userRepo.Object);
                services.AddSingleton(invitationRepo.Object);
                services.AddSingleton(prefsRepo.Object);
                services.AddSingleton(dashboardService.Object);
                services.AddSingleton(publicationService.Object);
                services.AddSingleton(projectRepo.Object);
                services.AddSingleton(systemStateMessageService.Object);
                services.AddSingleton(Mock.Of<ISectionRepository>());
                services.AddSingleton(Mock.Of<IUserService>());
                services.AddSingleton(Mock.Of<ISyncService>());
                services.AddSingleton(Mock.Of<IScrivenerProjectDiscoveryService>());
                services.AddSingleton(Mock.Of<IReaderAccessRepository>());
                services.AddSingleton(Mock.Of<ISyncProgressTracker>());
                services.AddSingleton(Mock.Of<IEmailSender>());
            });
        }

        private static string BuildTestConnectionString(string baseConnectionString, string databaseName)
        {
            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = databaseName
            };

            return builder.ConnectionString;
        }

        private static string LoadBaseConnectionString()
        {
            var webProjectRoot = FindWebProjectRoot();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(webProjectRoot)
                .AddJsonFile("appsettings.json", optional: false)
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            return configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "A PostgreSQL DefaultConnection is required for governing rendered email exposure tests.");
        }

        private static string FindWebProjectRoot()
        {
            var dir = Directory.GetCurrentDirectory();

            while (dir != null &&
                   !Directory.GetFiles(dir, "*.sln").Any() &&
                   !Directory.GetFiles(dir, "*.slnx").Any())
            {
                dir = Directory.GetParent(dir)?.FullName;
            }

            if (dir is null)
                throw new InvalidOperationException("Solution root not found for rendered privacy tests.");

            return Path.Combine(dir, "DraftView.Web");
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Task2TestAuth";
        public const string HeaderName = "X-Test-Auth";
        public const string AuthorMode = "Author";
        public static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var mode))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!string.Equals(mode, AuthorMode, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, AuthorId.ToString()),
                new Claim(ClaimTypes.Name, KnownEmail),
                new Claim(ClaimTypes.Role, Role.Author.ToString())
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
