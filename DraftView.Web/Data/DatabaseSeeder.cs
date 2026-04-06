using Microsoft.AspNetCore.Identity;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Infrastructure.Dropbox;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Web.Data;

public static class DatabaseSeeder
{
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
            logger.LogInformation("Author Identity user created: {Email}", authorEmail);
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
            logger.LogInformation("Support Identity user created: {Email}", supportEmail);
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
        var existingDomainUser = db.AppUsers.FirstOrDefault(u => u.Email == authorEmail);
        if (existingDomainUser is null)
        {
            var author = User.Create(authorEmail, authorDisplayName, Role.Author);
            author.Activate();
            db.AppUsers.Add(author);

            var prefs = UserNotificationPreferences.CreateForAuthor(
                author.Id, AuthorDigestMode.Immediate, null, "Europe/London");
            db.NotificationPreferences.Add(prefs);

            await db.SaveChangesAsync();
            logger.LogInformation("Author domain user created: {Email}", authorEmail);
        }

        // ---------------------------------------------------------------------------
        // Seed Support domain User
        // ---------------------------------------------------------------------------
        var existingSupportDomainUser = db.AppUsers.FirstOrDefault(u => u.Email == supportEmail);
        if (existingSupportDomainUser is null)
        {
            var support = User.Create(supportEmail, supportDisplayName, Role.SystemSupport);
            support.Activate();
            db.AppUsers.Add(support);

            await db.SaveChangesAsync();
            logger.LogInformation("Support domain user created: {Email}", supportEmail);
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
                var idUser = await userManager.FindByEmailAsync(du.Email);
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
                logger.LogWarning(ex, "Failed to backfill identity role for {Email}", du.Email);
            }
        }

        // ---------------------------------------------------------------------------
        // Seed DropboxConnection stub for author
        // If a legacy access token exists in config, seed it as connected.
        // The author should reconnect via OAuth to get a proper refresh token.
        // ---------------------------------------------------------------------------
        var authorUser = db.AppUsers.First(u => u.Email == authorEmail);
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
                    "Seeded legacy Dropbox access token for {Email}. " +
                    "Please reconnect via /dropbox/settings to get a proper refresh token.",
                    authorEmail);
            }

            db.DropboxConnections.Add(connection);
            await db.SaveChangesAsync();
            logger.LogInformation("DropboxConnection created for author {Email}", authorEmail);
        }

        // ---------------------------------------------------------------------------
        // Seed Test.scriv > Book 1 project
        // ---------------------------------------------------------------------------
        var existingProject = db.Projects.FirstOrDefault(p => p.Name == "Test - Book 1");
        if (existingProject is null)
        {
            var project = ScrivenerProject.Create(
                "Test - Book 1",
                scrivTestProjectDropboxPath,
                authorUser.Id,
                "DF1031AB-818A-41EB-AD49-F26D5C44F3D4");
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            logger.LogInformation("Test - Book 1 project created: {Path}", scrivTestProjectDropboxPath);
        }
    }
}
