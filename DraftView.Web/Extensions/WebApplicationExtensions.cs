using Microsoft.EntityFrameworkCore;
using DraftView.Infrastructure.Persistence;
using DraftView.Web.Data;
using DraftView.Domain.Enumerations;

namespace DraftView.Web.Extensions;

public static class WebApplicationExtensions
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        db.Database.Migrate();
        await Task.CompletedTask;
    }

    public static async Task SeedDatabaseAsync(this WebApplication app)
    {
        var cfg = app.Configuration;
        var seedEmail = cfg["Seed:AuthorEmail"] ?? "author@draftview.local";
        var seedPassword = cfg["Seed:AuthorPassword"] ?? "Password1!";
        var seedName = cfg["Seed:AuthorName"] ?? "Author";
        var seedPath = cfg["Seed:TestProjectPath"] ?? "/Apps/Scrivener/Test.scriv";
        var supportEmail = cfg["Seed:SupportEmail"] ?? "support@draftview.co.uk";
        var supportPassword = cfg["Seed:SupportPassword"] ?? "Password1!";
        var supportDisplayName = cfg["Seed:SupportName"] ?? "DraftView Support";

        await DatabaseSeeder.SeedAsync(
            app.Services,
            seedEmail,
            seedPassword,
            seedName,
            seedPath,
            supportEmail,
            supportPassword,
            supportDisplayName);
    }

    public static async Task ResetStaleSyncProjectsAsync(this WebApplication app)
    {
        using var startupScope = app.Services.CreateScope();
        var db = startupScope.ServiceProvider.GetRequiredService<DraftViewDbContext>();
        var stuckProjects = db.Projects
            .Where(p => p.SyncStatus == SyncStatus.Syncing)
            .ToList();
        foreach (var p in stuckProjects)
            p.UpdateSyncStatus(SyncStatus.Stale, DateTime.UtcNow, null);
        if (stuckProjects.Count > 0)
            await db.SaveChangesAsync();
    }
}
