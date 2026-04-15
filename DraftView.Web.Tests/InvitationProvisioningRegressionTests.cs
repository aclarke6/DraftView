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

public sealed class InvitationProvisioningRegressionTests :
    IClassFixture<InvitationProvisioningRegressionTests.InvitationProvisioningWebFactory>
{
    private readonly InvitationProvisioningWebFactory factory;

    public InvitationProvisioningRegressionTests(InvitationProvisioningWebFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task InviteReader_Post_Twice_SupersedesOlderPendingInvite_And_PersistsProtectedEmailFields()
    {
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        await LogInAsAuthorAsync(client);

        await PostInviteAsync(client, InvitationProvisioningWebFactory.ReaderEmail);
        await PostInviteAsync(client, InvitationProvisioningWebFactory.ReaderEmail);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();

        var invitedUser = await db.AppUsers
            .SingleAsync(u => u.DisplayName == "Pending" && u.Role == Role.BetaReader);

        Assert.False(invitedUser.IsActive);
        Assert.False(invitedUser.IsSoftDeleted);
        Assert.True(string.IsNullOrWhiteSpace(invitedUser.Email));
        Assert.False(string.IsNullOrWhiteSpace(invitedUser.EmailCiphertext));
        Assert.False(string.IsNullOrWhiteSpace(invitedUser.EmailLookupHmac));
        Assert.NotEqual(InvitationProvisioningWebFactory.ReaderEmail, invitedUser.EmailCiphertext);
        Assert.NotEqual(InvitationProvisioningWebFactory.ReaderEmail, invitedUser.EmailLookupHmac);

        var invitations = await db.Invitations
            .Where(i => i.UserId == invitedUser.Id)
            .OrderBy(i => i.IssuedAt)
            .ToListAsync();

        Assert.Equal(2, invitations.Count);
        Assert.Equal(InvitationStatus.Cancelled, invitations[0].Status);
        Assert.Equal(InvitationStatus.Pending, invitations[1].Status);
        Assert.NotEqual(invitations[0].Token, invitations[1].Token);

        var preferencesCount = await db.UserPreferences.CountAsync(p => p.UserId == invitedUser.Id);
        Assert.Equal(1, preferencesCount);
    }

    private static async Task LogInAsAuthorAsync(HttpClient client)
    {
        var loginGet = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginGet.StatusCode);

        var loginHtml = await loginGet.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(loginHtml);

        var loginPost = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = InvitationProvisioningWebFactory.AuthorEmail,
                ["Password"] = InvitationProvisioningWebFactory.AuthorPassword,
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = antiforgeryToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, loginPost.StatusCode);
        Assert.Equal("/Author/Dashboard", loginPost.Headers.Location?.OriginalString);
    }

    private static async Task PostInviteAsync(HttpClient client, string email)
    {
        var inviteGet = await client.GetAsync("/Author/InviteReader");
        Assert.Equal(HttpStatusCode.OK, inviteGet.StatusCode);

        var inviteHtml = await inviteGet.Content.ReadAsStringAsync();
        var antiforgeryToken = ExtractAntiforgeryToken(inviteHtml);

        var formData = new List<KeyValuePair<string, string>>
        {
            new("Email", email),
            new("NeverExpires", "true"),
            new("NeverExpires", "false"),
            new("__RequestVerificationToken", antiforgeryToken)
        };

        var invitePost = await client.PostAsync(
            "/Author/InviteReader",
            new FormUrlEncodedContent(formData));

        if (invitePost.StatusCode != HttpStatusCode.Redirect)
        {
            var invitePostHtml = await invitePost.Content.ReadAsStringAsync();
            Assert.Fail(
                $"Expected redirect after posting invitation form, but got {(int)invitePost.StatusCode} {invitePost.StatusCode}.{Environment.NewLine}{invitePostHtml}");
        }

        Assert.Equal(HttpStatusCode.Redirect, invitePost.StatusCode);
        Assert.Equal("/Author/Readers", invitePost.Headers.Location?.OriginalString);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.True(match.Success, "Expected page to render an antiforgery token.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public sealed class InvitationProvisioningWebFactory : WebApplicationFactory<Program>
    {
        public const string AuthorEmail = "invite.regression.author@example.test";
        public const string AuthorPassword = "Password1!";
        public const string ReaderEmail = "invite.regression.reader@example.test";

        private const string DatabaseName = "draftview_webtests_invitation_provisioning";
        private bool initialized;

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

            var author = User.Create(AuthorEmail, "Invitation Regression Author", Role.Author);
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
                    "A PostgreSQL DefaultConnection is required for invitation provisioning regression tests.");
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
                throw new InvalidOperationException("Solution root not found for invitation provisioning regression tests.");

            return Path.Combine(dir, "DraftView.Web");
        }
    }
}
