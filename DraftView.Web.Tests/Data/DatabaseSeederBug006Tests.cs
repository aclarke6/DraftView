using DraftView.Application.Interfaces;
using Xunit;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Dropbox;
using DraftView.Infrastructure.Persistence;
using DraftView.Infrastructure.Security;
using DraftView.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraftView.Web.Tests.Data;

public class DatabaseSeederBug006Tests
{
    private static readonly byte[] EncryptionKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] HmacKey = Enumerable.Range(33, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public async Task SeedAsync_WithCorruptAuthorProtectedFields_RepairsAuthorRowInsteadOfLeavingInvalidCiphertext()
    {
        var services = BuildServiceProvider(Guid.NewGuid().ToString());
        const string authorEmail = "bug006.author@example.test";
        const string authorPassword = "Password1!";

        await SeedExistingIdentityAndDomainAuthorWithCorruptProtectedFieldsAsync(services, authorEmail, authorPassword);

        await DatabaseSeeder.SeedAsync(
            services,
            authorEmail,
            authorPassword,
            "Bug 006 Author",
            "/Apps/Scrivener/Test.scriv",
            "support.bug006@example.test",
            "Password1!",
            "Support User");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IUserEmailEncryptionService>();
        var hmacService = scope.ServiceProvider.GetRequiredService<IUserEmailLookupHmacService>();

        var identityAuthor = await userManager.FindByEmailAsync(authorEmail);
        Assert.NotNull(identityAuthor);

        var author = await db.AppUsers.SingleAsync(u => u.Id == Guid.Parse(identityAuthor!.Id));

        Assert.DoesNotContain("PENDING-", author.EmailCiphertext, StringComparison.Ordinal);
        Assert.DoesNotContain("PENDING-", author.EmailLookupHmac, StringComparison.Ordinal);
        Assert.Equal(authorEmail, encryptionService.Decrypt(author.EmailCiphertext));
        Assert.Equal(hmacService.Compute(DraftViewDbContext.NormalizeEmail(authorEmail)), author.EmailLookupHmac);
    }

    [Fact]
    public async Task SeedAsync_WhenCreatingNewAuthor_WritesDecryptableProtectedEmail()
    {
        var services = BuildServiceProvider(Guid.NewGuid().ToString());
        const string authorEmail = "new.author.bug006@example.test";

        await DatabaseSeeder.SeedAsync(
            services,
            authorEmail,
            "Password1!",
            "New Bug 006 Author",
            "/Apps/Scrivener/Test.scriv",
            "support.new.bug006@example.test",
            "Password1!",
            "Support User");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IUserEmailEncryptionService>();

        var author = await db.AppUsers.SingleAsync(u => u.Role == Role.Author);
        var decrypted = encryptionService.Decrypt(author.EmailCiphertext);

        Assert.Equal(authorEmail, decrypted);
        Assert.False(string.IsNullOrWhiteSpace(author.EmailLookupHmac));
    }

    [Fact]
    public async Task RepairDuplicateAuthorRowsAsync_WhenDuplicateAuthorRowsExist_RepointsDependentsAndDeletesDuplicate()
    {
        var services = BuildServiceProvider(Guid.NewGuid().ToString());
        const string authorEmail = "duplicate.author.bug006@example.test";
        const string authorPassword = "Password1!";

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(Role.Author.ToString()))
                await roleManager.CreateAsync(new IdentityRole(Role.Author.ToString()));

            var canonicalAuthor = User.Create(authorEmail, "Canonical Author", Role.Author);
            canonicalAuthor.Activate();
            db.AppUsers.Add(canonicalAuthor);

            var duplicateAuthor = User.Create("duplicate.row@example.test", "Duplicate Author", Role.Author);
            duplicateAuthor.Activate();
            db.AppUsers.Add(duplicateAuthor);

            db.DropboxConnections.Add(DropboxConnection.CreateStub(duplicateAuthor.Id));
            db.UserPreferences.Add(UserPreferences.CreateForAuthor(
                duplicateAuthor.Id,
                AuthorDigestMode.Immediate,
                null,
                "Europe/London"));
            db.AuthorNotifications.Add(AuthorNotification.Create(
                duplicateAuthor.Id,
                DraftView.Domain.Notifications.NotificationEventType.SyncCompleted,
                "Duplicate Notification",
                null,
                null,
                DateTime.UtcNow));
            db.Projects.Add(Project.Create(
                "Duplicate Author Project",
                "/Apps/Scrivener/Duplicate.scriv",
                duplicateAuthor.Id,
                "sync-root-duplicate"));

            await db.SaveChangesAsync();

            var identityUser = new IdentityUser
            {
                Id = canonicalAuthor.Id.ToString(),
                UserName = authorEmail,
                Email = authorEmail,
                EmailConfirmed = true
            };

            var createIdentity = await userManager.CreateAsync(identityUser, authorPassword);
            Assert.True(createIdentity.Succeeded, string.Join(", ", createIdentity.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(identityUser, Role.Author.ToString());
        }

        await DatabaseSeeder.RepairDuplicateAuthorRowsAsync(services, authorEmail);

        using var assertScope = services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        var canonicalIdentityId = Guid.Parse((await assertScope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>()
            .FindByEmailAsync(authorEmail))!.Id);

        var authors = await assertDb.AppUsers.Where(u => u.Role == Role.Author).ToListAsync();
        Assert.Single(authors);
        Assert.Equal(canonicalIdentityId, authors[0].Id);

        Assert.All(assertDb.DropboxConnections, c => Assert.Equal(canonicalIdentityId, c.UserId));
        Assert.All(assertDb.UserPreferences, p => Assert.Equal(canonicalIdentityId, p.UserId));
        Assert.All(assertDb.AuthorNotifications, n => Assert.Equal(canonicalIdentityId, n.AuthorId));
        Assert.All(assertDb.Projects, p => Assert.Equal(canonicalIdentityId, p.AuthorId));
    }

    private static async Task SeedExistingIdentityAndDomainAuthorWithCorruptProtectedFieldsAsync(
        IServiceProvider services,
        string authorEmail,
        string authorPassword)
    {
        Guid authorId;

        using (var setupScope = services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = setupScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(Role.Author.ToString()))
                await roleManager.CreateAsync(new IdentityRole(Role.Author.ToString()));

            var domainAuthor = User.Create(authorEmail, "Existing Author", Role.Author);
            domainAuthor.Activate();
            db.AppUsers.Add(domainAuthor);
            await db.SaveChangesAsync();
            authorId = domainAuthor.Id;

            var identityUser = new IdentityUser
            {
                Id = domainAuthor.Id.ToString(),
                UserName = authorEmail,
                Email = authorEmail,
                EmailConfirmed = true
            };

            var createIdentity = await userManager.CreateAsync(identityUser, authorPassword);
            Assert.True(createIdentity.Succeeded, string.Join(", ", createIdentity.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(identityUser, Role.Author.ToString());
        }

        using (var mutateScope = services.CreateScope())
        {
            var db = mutateScope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
            var persistedAuthor = await db.AppUsers.SingleAsync(u => u.Id == authorId);
            persistedAuthor.SetProtectedEmail("PENDING-CIPHERTEXT:AUTHOR", "PENDING-HMAC:AUTHOR");
            await db.SaveChangesAsync();
        }
    }

    private static ServiceProvider BuildServiceProvider(string databaseName)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<DraftViewDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IUserEmailEncryptionService>(_ => new UserEmailEncryptionService(EncryptionKey));
        services.AddScoped<IUserEmailLookupHmacService>(_ => new UserEmailLookupHmacService(HmacKey));
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<DraftViewDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(new DropboxClientSettings());

        return services.BuildServiceProvider();
    }
}
