using Microsoft.AspNetCore.Identity;
using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Infrastructure.Persistence;

namespace ScrivenerSync.Web.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        string authorEmail,
        string authorPassword,
        string authorDisplayName,
        string scrivTestProjectDropboxPath)
    {
        using var scope        = services.CreateScope();
        var db                 = scope.ServiceProvider.GetRequiredService<ScrivenerSyncDbContext>();
        var userManager        = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var logger             = scope.ServiceProvider.GetRequiredService<ILogger<ScrivenerSyncDbContext>>();

        // ---------------------------------------------------------------------------
        // Seed Author IdentityUser (for login)
        // ---------------------------------------------------------------------------
        var existingIdentityUser = await userManager.FindByEmailAsync(authorEmail);
        if (existingIdentityUser is null)
        {
            var identityUser = new IdentityUser
            {
                UserName       = authorEmail,
                Email          = authorEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(identityUser, authorPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create author Identity user: {errors}");
            }

            logger.LogInformation("Author Identity user created: {Email}", authorEmail);
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
        // Seed Test.scriv project
        // ---------------------------------------------------------------------------
        var existingProject = db.Projects.FirstOrDefault(p => p.Name == "Test");
        if (existingProject is null)
        {
            var project = ScrivenerProject.Create("Test", scrivTestProjectDropboxPath);
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            logger.LogInformation("Test project created: {Path}", scrivTestProjectDropboxPath);
        }
    }
}
