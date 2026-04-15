using System.Net;
using System.Text.RegularExpressions;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Persistence;
using DraftView.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;
using DraftView.Web.Services;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Web.Tests;

public sealed class AccountLoginRegressionTests :
    IClassFixture<AccountLoginRegressionTests.LoginWebFactory>
{
    private readonly LoginWebFactory factory;

    public AccountLoginRegressionTests(LoginWebFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Account_Login_Post_WithValidAuthorCredentials_RedirectsToAuthorDashboard_AndAuthenticatesSession()
    {
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var getLogin = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, getLogin.StatusCode);

        var loginHtml = await getLogin.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(loginHtml);

        var postLogin = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = LoginWebFactory.AuthorEmail,
                ["Password"] = LoginWebFactory.AuthorPassword,
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, postLogin.StatusCode);
        Assert.NotNull(postLogin.Headers.Location);
        Assert.Equal("/Author/Dashboard", postLogin.Headers.Location!.OriginalString);

        var dashboard = await client.GetAsync("/Author/Dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);

        var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        Assert.Contains("Dashboard", dashboardHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Login Regression Author", dashboardHtml, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.True(match.Success, "Expected login page to render an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public sealed class LoginWebFactory : WebApplicationFactory<Program>
    {
        public const string AuthorEmail = "login.regression.author@example.test";
        public const string AuthorPassword = "Password1!";

        private const string DatabaseName = "draftview_webtests_login";
        private bool initialized;
        private string? testConnectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                var baseConnectionString = LoadBaseConnectionString();

                testConnectionString = BuildTestConnectionString(baseConnectionString, DatabaseName);

                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = testConnectionString,
                    ["Email:Provider"] = "Console"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IEmailSender>();
                services.AddScoped<IEmailSender, ConsoleEmailSender>();
            });
        }

        public async Task InitializeDatabaseAsync()
        {
            if (initialized)
                return;

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();

            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            await SeedAuthorAsync(scope.ServiceProvider, db);
            initialized = true;
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
                    "A PostgreSQL DefaultConnection is required for web login regression tests.");
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
                throw new InvalidOperationException("Solution root not found for login regression tests.");

            return Path.Combine(dir, "DraftView.Web");
        }
        private static async Task SeedAuthorAsync(IServiceProvider services, DraftViewDbContext db)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

            if (!await roleManager.RoleExistsAsync(Role.Author.ToString()))
            {
                var createRole = await roleManager.CreateAsync(new IdentityRole(Role.Author.ToString()));
                Assert.True(
                    createRole.Succeeded,
                    $"Failed to create author role: {string.Join(", ", createRole.Errors.Select(e => e.Description))}");
            }

            var author = User.Create(AuthorEmail, "Login Regression Author", Role.Author);
            author.Activate();
            db.AppUsers.Add(author);
            db.UserPreferences.Add(UserPreferences.CreateForAuthor(
                author.Id,
                AuthorDigestMode.Immediate,
                null,
                "Europe/London"));
            await db.SaveChangesAsync();

            var identityUser = new IdentityUser
            {
                Id = author.Id.ToString(),
                UserName = AuthorEmail,
                Email = AuthorEmail,
                EmailConfirmed = true
            };

            var createUser = await userManager.CreateAsync(identityUser, AuthorPassword);
            Assert.True(
                createUser.Succeeded,
                $"Failed to create identity user: {string.Join(", ", createUser.Errors.Select(e => e.Description))}");

            var addToRole = await userManager.AddToRoleAsync(identityUser, Role.Author.ToString());
            Assert.True(
                addToRole.Succeeded,
                $"Failed to add identity user to author role: {string.Join(", ", addToRole.Errors.Select(e => e.Description))}");
        }
    }
}
