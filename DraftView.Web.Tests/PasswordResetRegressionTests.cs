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

public sealed class PasswordResetRegressionTests :
    IClassFixture<PasswordResetRegressionTests.PasswordResetWebFactory>
{
    private readonly PasswordResetWebFactory factory;

    public PasswordResetRegressionTests(PasswordResetWebFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_ThenResetPassword_BindsTokenToUser_And_AllowsLoginWithNewPassword()
    {
        await factory.InitializeDatabaseAsync();

        Guid readerUserId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            readerUserId = await db.AppUsers
                .Where(u => u.DisplayName == "Password Reset Reader")
                .Select(u => u.Id)
                .SingleAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var forgotPasswordGet = await client.GetAsync("/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.OK, forgotPasswordGet.StatusCode);

        var forgotPasswordHtml = await forgotPasswordGet.Content.ReadAsStringAsync();
        var forgotPasswordToken = ExtractAntiforgeryToken(forgotPasswordHtml);

        var forgotPasswordPost = await client.PostAsync(
            "/Account/ForgotPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = PasswordResetWebFactory.ReaderEmail,
                ["__RequestVerificationToken"] = forgotPasswordToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, forgotPasswordPost.StatusCode);
        Assert.NotNull(forgotPasswordPost.Headers.Location);
        Assert.Equal("/Account/ForgotPasswordConfirmation", forgotPasswordPost.Headers.Location!.OriginalString);

        string resetTokenValue;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            var resetToken = await db.PasswordResetTokens
                .Where(t => t.UserId == readerUserId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            Assert.Equal(readerUserId, resetToken.UserId);
            resetTokenValue = resetToken.Token;
        }

        var resetPasswordGet = await client.GetAsync($"/Account/ResetPassword?token={Uri.EscapeDataString(resetTokenValue)}");
        Assert.Equal(HttpStatusCode.OK, resetPasswordGet.StatusCode);

        var resetPasswordHtml = await resetPasswordGet.Content.ReadAsStringAsync();
        var resetPasswordToken = ExtractAntiforgeryToken(resetPasswordHtml);

        var newPassword = "NewPassword1";
        var resetPasswordPost = await client.PostAsync(
            "/Account/ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Token"] = resetTokenValue,
                ["Password"] = newPassword,
                ["ConfirmPassword"] = newPassword,
                ["__RequestVerificationToken"] = resetPasswordToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, resetPasswordPost.StatusCode);
        Assert.NotNull(resetPasswordPost.Headers.Location);
        Assert.Equal("/Account/Login", resetPasswordPost.Headers.Location!.OriginalString);

        var loginGet = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginGet.StatusCode);

        var loginHtml = await loginGet.Content.ReadAsStringAsync();
        var loginToken = ExtractAntiforgeryToken(loginHtml);

        var loginPost = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = PasswordResetWebFactory.ReaderEmail,
                ["Password"] = newPassword,
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = loginToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, loginPost.StatusCode);
        Assert.NotNull(loginPost.Headers.Location);
        Assert.Equal("/Reader/Dashboard", loginPost.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ForgotPassword_WithMismatchedDomainAndIdentityIds_UsesValidLinkAndAllowsPasswordReset()
    {
        await factory.InitializeDatabaseAsync();

        Guid readerUserId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            readerUserId = await db.AppUsers
                .Where(u => u.DisplayName == "Password Reset Mismatch Reader")
                .Select(u => u.Id)
                .SingleAsync();
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var forgotPasswordGet = await client.GetAsync("/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.OK, forgotPasswordGet.StatusCode);

        var forgotPasswordHtml = await forgotPasswordGet.Content.ReadAsStringAsync();
        var forgotPasswordToken = ExtractAntiforgeryToken(forgotPasswordHtml);

        var forgotPasswordPost = await client.PostAsync(
            "/Account/ForgotPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = PasswordResetWebFactory.MismatchedReaderEmail,
                ["__RequestVerificationToken"] = forgotPasswordToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, forgotPasswordPost.StatusCode);
        Assert.NotNull(forgotPasswordPost.Headers.Location);
        Assert.Equal("/Account/ForgotPasswordConfirmation", forgotPasswordPost.Headers.Location!.OriginalString);

        string resetTokenValue;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            var resetToken = await db.PasswordResetTokens
                .Where(t => t.UserId == readerUserId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();

            Assert.False(resetToken.IsUsed);
            Assert.True(resetToken.IsValid());
            resetTokenValue = resetToken.Token;
        }

        var resetPasswordGet = await client.GetAsync($"/Account/ResetPassword?token={Uri.EscapeDataString(resetTokenValue)}");
        Assert.Equal(HttpStatusCode.OK, resetPasswordGet.StatusCode);

        var resetPasswordHtml = await resetPasswordGet.Content.ReadAsStringAsync();
        Assert.DoesNotContain("/Account/ResetPasswordInvalid", resetPasswordHtml, StringComparison.OrdinalIgnoreCase);
        var resetPasswordToken = ExtractAntiforgeryToken(resetPasswordHtml);

        var newPassword = "NewPassword2";
        var resetPasswordPost = await client.PostAsync(
            "/Account/ResetPassword",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Token"] = resetTokenValue,
                ["Password"] = newPassword,
                ["ConfirmPassword"] = newPassword,
                ["__RequestVerificationToken"] = resetPasswordToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, resetPasswordPost.StatusCode);
        Assert.NotNull(resetPasswordPost.Headers.Location);
        Assert.Equal("/Account/Login", resetPasswordPost.Headers.Location!.OriginalString);

        var loginGet = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginGet.StatusCode);

        var loginHtml = await loginGet.Content.ReadAsStringAsync();
        var loginToken = ExtractAntiforgeryToken(loginHtml);

        var loginPost = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = PasswordResetWebFactory.MismatchedReaderEmail,
                ["Password"] = newPassword,
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = loginToken
            }));

        Assert.Equal(HttpStatusCode.Redirect, loginPost.StatusCode);
        Assert.NotNull(loginPost.Headers.Location);
        Assert.Equal("/Reader/Dashboard", loginPost.Headers.Location!.OriginalString);
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

    public sealed class PasswordResetWebFactory : WebApplicationFactory<Program>
    {
        public const string ReaderEmail = "password.reset.reader@example.test";
        public const string ReaderPassword = "ReaderPassword1";
        public const string MismatchedReaderEmail = "password.reset.mismatch@example.test";
        public const string MismatchedReaderPassword = "ReaderPassword2";

        private const string DatabaseName = "draftview_webtests_passwordreset";
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

            await SeedReaderAsync(scope.ServiceProvider, db);
            await SeedReaderWithMismatchedIdentityIdAsync(scope.ServiceProvider, db);
            initialized = true;
        }

        private static async Task SeedReaderAsync(IServiceProvider services, DraftViewDbContext db)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

            if (!await roleManager.RoleExistsAsync(Role.BetaReader.ToString()))
            {
                var createRole = await roleManager.CreateAsync(new IdentityRole(Role.BetaReader.ToString()));
                Assert.True(
                    createRole.Succeeded,
                    $"Failed to create reader role: {string.Join(", ", createRole.Errors.Select(e => e.Description))}");
            }

            var reader = User.Create(ReaderEmail, "Password Reset Reader", Role.BetaReader);
            reader.Activate();
            db.AppUsers.Add(reader);
            db.UserPreferences.Add(UserPreferences.CreateForBetaReader(reader.Id));
            await db.SaveChangesAsync();

            var identityUser = new IdentityUser
            {
                Id = reader.Id.ToString(),
                UserName = ReaderEmail,
                Email = ReaderEmail,
                EmailConfirmed = true
            };

            var createUser = await userManager.CreateAsync(identityUser, ReaderPassword);
            Assert.True(
                createUser.Succeeded,
                $"Failed to create identity user: {string.Join(", ", createUser.Errors.Select(e => e.Description))}");

            var addToRole = await userManager.AddToRoleAsync(identityUser, Role.BetaReader.ToString());
            Assert.True(
                addToRole.Succeeded,
                $"Failed to add identity user to reader role: {string.Join(", ", addToRole.Errors.Select(e => e.Description))}");
        }

        private static async Task SeedReaderWithMismatchedIdentityIdAsync(IServiceProvider services, DraftViewDbContext db)
        {
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

            var reader = User.Create(MismatchedReaderEmail, "Password Reset Mismatch Reader", Role.BetaReader);
            reader.Activate();
            db.AppUsers.Add(reader);
            db.UserPreferences.Add(UserPreferences.CreateForBetaReader(reader.Id));
            await db.SaveChangesAsync();

            var identityUser = new IdentityUser
            {
                UserName = MismatchedReaderEmail,
                Email = MismatchedReaderEmail,
                EmailConfirmed = true
            };

            var createUser = await userManager.CreateAsync(identityUser, MismatchedReaderPassword);
            Assert.True(
                createUser.Succeeded,
                $"Failed to create mismatched identity user: {string.Join(", ", createUser.Errors.Select(e => e.Description))}");

            Assert.NotEqual(reader.Id.ToString(), identityUser.Id);

            var addToRole = await userManager.AddToRoleAsync(identityUser, Role.BetaReader.ToString());
            Assert.True(
                addToRole.Succeeded,
                $"Failed to add mismatched identity user to reader role: {string.Join(", ", addToRole.Errors.Select(e => e.Description))}");
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
                    "A PostgreSQL DefaultConnection is required for password reset regression tests.");
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
                throw new InvalidOperationException("Solution root not found for password reset regression tests.");

            return Path.Combine(dir, "DraftView.Web");
        }
    }
}
