using Microsoft.AspNetCore.Identity;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Application.Interfaces;
using DraftView.Infrastructure.Dropbox;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Web.Data;

public static class DatabaseSeeder
{
    public static async Task RepairDuplicateAuthorRowsAsync(IServiceProvider services, string authorEmail)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DraftViewDbContext>>();

        var existingIdentityUser = await userManager.FindByEmailAsync(authorEmail);
        if (existingIdentityUser is null)
            return;

        var identityAuthorId = Guid.Parse(existingIdentityUser.Id);
        var authorRows = db.AppUsers
            .Where(u => u.Role == Role.Author)
            .OrderBy(u => u.CreatedAt)
            .ToList();

        if (authorRows.Count <= 1)
            return;

        var canonicalAuthor = authorRows.FirstOrDefault(u => u.Id == identityAuthorId)
            ?? authorRows.First();

        var duplicateAuthors = authorRows
            .Where(u => u.Id != canonicalAuthor.Id)
            .ToList();

        foreach (var duplicate in duplicateAuthors)
        {
            RepointDropboxConnection(db, duplicate.Id, canonicalAuthor.Id);
            RepointUserPreferences(db, duplicate.Id, canonicalAuthor.Id);
            RepointAuthorNotifications(db, duplicate.Id, canonicalAuthor.Id);
            RepointProjects(db, duplicate.Id, canonicalAuthor.Id);
            RepointReaderAccess(db, duplicate.Id, canonicalAuthor.Id);
            db.AppUsers.Remove(duplicate);
        }

        canonicalAuthor.LoadEmailForRuntime(authorEmail);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Duplicate author-row repair completed. Canonical AuthorId {AuthorId}. Removed duplicates: {DuplicateCount}",
            canonicalAuthor.Id,
            duplicateAuthors.Count);
    }

    public static async Task SeedAsync(
        IServiceProvider services,
        string authorEmail,
        string authorPassword,
        string authorDisplayName,
        string scrivTestProjectDropboxPath,
        string supportEmail,
        string supportPassword,
        string supportDisplayName)
    {
        using var scope        = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DraftViewDbContext>>();
        var dropboxSettings = scope.ServiceProvider.GetRequiredService<DropboxClientSettings>();
        var emailEncryptionService = scope.ServiceProvider.GetRequiredService<IUserEmailEncryptionService>();
        var emailLookupHmacService = scope.ServiceProvider.GetRequiredService<IUserEmailLookupHmacService>();

        // ---------------------------------------------------------------------------
        // Seed Identity roles
        // ---------------------------------------------------------------------------
        if (!await roleManager.RoleExistsAsync(Role.Author.ToString()))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(Role.Author.ToString()));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create role {Role.Author}: {errors}");
            }
        }

        if (!await roleManager.RoleExistsAsync(Role.SystemSupport.ToString()))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(Role.SystemSupport.ToString()));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create role {Role.SystemSupport}: {errors}");
            }
        }

        // Ensure BetaReader role exists for backfill/membership sync
        if (!await roleManager.RoleExistsAsync(Role.BetaReader.ToString()))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(Role.BetaReader.ToString()));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create role {Role.BetaReader}: {errors}");
            }
        }

        // ---------------------------------------------------------------------------
        // Seed Author IdentityUser (for login)
        // ---------------------------------------------------------------------------
        var existingIdentityUser = await userManager.FindByEmailAsync(authorEmail);
        if (existingIdentityUser is null)
        {
            var identityUser = new IdentityUser {
                UserName = authorEmail,
                Email = authorEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(identityUser, authorPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create author Identity user: {errors}");
            }

            existingIdentityUser = identityUser;
            logger.LogInformation("Author Identity user created.");
        }

        if (!await userManager.IsInRoleAsync(existingIdentityUser, Role.Author.ToString()))
        {
            var result = await userManager.AddToRoleAsync(existingIdentityUser, Role.Author.ToString());
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign author role: {errors}");
            }
        }

        // ---------------------------------------------------------------------------
        // Seed Support IdentityUser (for login)
        // ---------------------------------------------------------------------------
        var existingSupportIdentityUser = await userManager.FindByEmailAsync(supportEmail);
        if (existingSupportIdentityUser is null)
        {
            var identityUser = new IdentityUser {
                UserName = supportEmail,
                Email = supportEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(identityUser, supportPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create support Identity user: {errors}");
            }

            existingSupportIdentityUser = identityUser;
            logger.LogInformation("Support Identity user created.");
        }

        if (!await userManager.IsInRoleAsync(existingSupportIdentityUser, Role.SystemSupport.ToString()))
        {
            var result = await userManager.AddToRoleAsync(existingSupportIdentityUser, Role.SystemSupport.ToString());
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign support role: {errors}");
            }
        }

        // ---------------------------------------------------------------------------
        // Seed Author domain User
        // ---------------------------------------------------------------------------
        var identityAuthorId = Guid.Parse(existingIdentityUser.Id);
        var existingDomainUser = db.AppUsers.FirstOrDefault(u => u.Id == identityAuthorId);
        if (existingDomainUser is null)
        {
            var authorRows = db.AppUsers
                .Where(u => u.Role == Role.Author)
                .OrderBy(u => u.CreatedAt)
                .ToList();

            existingDomainUser = authorRows.FirstOrDefault();
        }

        if (existingDomainUser is null)
        {
            var author = User.Create(authorEmail, authorDisplayName, Role.Author);
            author.Activate();
            db.AppUsers.Add(author);

            var prefs = UserPreferences.CreateForAuthor(
                author.Id, AuthorDigestMode.Immediate, null, "Europe/London");
            db.UserPreferences.Add(prefs);

            await db.SaveChangesAsync();
            logger.LogInformation("Author domain user created with user ID {UserId}", author.Id);

            existingDomainUser = author;
        }
        else if (HasInvalidCiphertext(existingDomainUser.EmailCiphertext))
        {
            existingDomainUser.LoadEmailForRuntime(authorEmail);
            await db.SaveChangesAsync();
            logger.LogInformation("Author domain user repaired for user ID {UserId}", existingDomainUser.Id);
        }

        // ---------------------------------------------------------------------------
        // Seed Support domain User
        // ---------------------------------------------------------------------------
        var supportLookup = ComputeLookup(supportEmail, emailLookupHmacService);
        var existingSupportDomainUser = db.AppUsers.FirstOrDefault(u => u.EmailLookupHmac == supportLookup);
        if (existingSupportDomainUser is null)
        {
            var support = User.Create(supportEmail, supportDisplayName, Role.SystemSupport);
            support.Activate();
            db.AppUsers.Add(support);

            await db.SaveChangesAsync();
            logger.LogInformation("Support domain user created with user ID {UserId}", support.Id);
        }

        // ---------------------------------------------------------------------------
        // Backfill Identity role membership for existing domain users (map by email)
        // This ensures Identity roles reflect the domain `AppUsers.Role` values.
        // Safe to run repeatedly — userManager.AddToRoleAsync is idempotent for existing membership.
        // ---------------------------------------------------------------------------
        var allDomainUsers = db.AppUsers.ToList();
        foreach (var du in allDomainUsers)
        {
            try
            {
                var runtimeEmail = LoadRuntimeEmail(du, emailEncryptionService);
                var idUser = await userManager.FindByEmailAsync(runtimeEmail);
                if (idUser is null) continue;

                var roleName = du.Role.ToString();
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }

                if (!await userManager.IsInRoleAsync(idUser, roleName))
                {
                    await userManager.AddToRoleAsync(idUser, roleName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to backfill identity role for user {UserId} with role {Role}", du.Id, du.Role);
            }
        }

        // ---------------------------------------------------------------------------
        // Seed DropboxConnection stub for author
        // If a legacy access token exists in config, seed it as connected.
        // The author should reconnect via OAuth to get a proper refresh token.
        // ---------------------------------------------------------------------------
        var authorUser = existingDomainUser;
        if (string.IsNullOrWhiteSpace(authorUser.Email))
            authorUser.LoadEmailForRuntime(authorEmail);

        var existingConnection = db.DropboxConnections.FirstOrDefault(d => d.UserId == authorUser.Id);
        if (existingConnection is null)
        {
            var connection = DropboxConnection.CreateStub(authorUser.Id);

            if (!string.IsNullOrWhiteSpace(dropboxSettings.AccessToken))
            {
                // Seed legacy token â€” no refresh token available, will need reconnect when expired
                connection.Authorise(
                    dropboxSettings.AccessToken,
                    "legacy-no-refresh-token",
                    DateTime.UtcNow.AddDays(1)); // conservative expiry â€” prompt reconnect soon
                logger.LogWarning(
                    "Seeded legacy Dropbox access token for user {UserId}. " +
                    "Please reconnect via /dropbox/settings to get a proper refresh token.",
                    authorUser.Id);
            }

            db.DropboxConnections.Add(connection);
            await db.SaveChangesAsync();
            logger.LogInformation("DropboxConnection created for author {UserId}", authorUser.Id);
        }

        // ---------------------------------------------------------------------------
        // Seed Test.scriv > Book 1 project
        // ---------------------------------------------------------------------------
        var existingProject = db.Projects.FirstOrDefault(p => p.Name == "Test - Book 1");
        if (existingProject is null)
        {
            var project = Project.Create(
                "Test - Book 1",
                scrivTestProjectDropboxPath,
                authorUser.Id,
                "DF1031AB-818A-41EB-AD49-F26D5C44F3D4");
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            logger.LogInformation("Test - Book 1 project created: {Path}", scrivTestProjectDropboxPath);
        }
    }

    private static string ComputeLookup(string email, IUserEmailLookupHmacService emailLookupHmacService) =>
        emailLookupHmacService.Compute(DraftViewDbContext.NormalizeEmail(email));

    private static string LoadRuntimeEmail(User user, IUserEmailEncryptionService emailEncryptionService)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
            return user.Email;

        var runtimeEmail = emailEncryptionService.Decrypt(user.EmailCiphertext);
        user.LoadEmailForRuntime(runtimeEmail);
        return runtimeEmail;
    }

    private static bool HasInvalidCiphertext(string emailCiphertext)
    {
        if (string.IsNullOrWhiteSpace(emailCiphertext))
            return true;

        if (emailCiphertext.StartsWith("PENDING-", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            Convert.FromBase64String(emailCiphertext);
            return false;
        }
        catch (FormatException)
        {
            return true;
        }
    }

    private static void RepointDropboxConnection(DraftViewDbContext db, Guid fromAuthorId, Guid toAuthorId)
    {
        var canonical = db.DropboxConnections.FirstOrDefault(c => c.UserId == toAuthorId);
        var duplicate = db.DropboxConnections.FirstOrDefault(c => c.UserId == fromAuthorId);

        if (duplicate is null)
            return;

        if (canonical is not null)
        {
            db.DropboxConnections.Remove(duplicate);
            return;
        }

        db.Entry(duplicate).Property(nameof(DropboxConnection.UserId)).CurrentValue = toAuthorId;
    }

    private static void RepointUserPreferences(DraftViewDbContext db, Guid fromAuthorId, Guid toAuthorId)
    {
        var canonical = db.UserPreferences.FirstOrDefault(p => p.UserId == toAuthorId);
        var duplicate = db.UserPreferences.FirstOrDefault(p => p.UserId == fromAuthorId);

        if (duplicate is null)
            return;

        if (canonical is not null)
        {
            db.UserPreferences.Remove(duplicate);
            return;
        }

        db.Entry(duplicate).Property(nameof(UserPreferences.UserId)).CurrentValue = toAuthorId;
    }

    private static void RepointAuthorNotifications(DraftViewDbContext db, Guid fromAuthorId, Guid toAuthorId)
    {
        var notifications = db.AuthorNotifications
            .Where(n => n.AuthorId == fromAuthorId)
            .ToList();

        foreach (var notification in notifications)
            db.Entry(notification).Property(nameof(AuthorNotification.AuthorId)).CurrentValue = toAuthorId;
    }

    private static void RepointProjects(DraftViewDbContext db, Guid fromAuthorId, Guid toAuthorId)
    {
        var projects = db.Projects
            .Where(p => p.AuthorId == fromAuthorId)
            .ToList();

        foreach (var project in projects)
            db.Entry(project).Property(nameof(Project.AuthorId)).CurrentValue = toAuthorId;
    }

    private static void RepointReaderAccess(DraftViewDbContext db, Guid fromAuthorId, Guid toAuthorId)
    {
        var readerAccessRows = db.ReaderAccess
            .Where(r => r.AuthorId == fromAuthorId)
            .ToList();

        foreach (var readerAccess in readerAccessRows)
            db.Entry(readerAccess).Property(nameof(ReaderAccess.AuthorId)).CurrentValue = toAuthorId;
    }
}
