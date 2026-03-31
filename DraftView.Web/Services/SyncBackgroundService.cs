using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Web.Services;

public class SyncBackgroundService(
    IServiceProvider serviceProvider,
    DraftViewSettings settings,
    ILogger<SyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Sync background service started. Interval: {Interval} minutes.",
            settings.SyncIntervalMinutes);

        // Wait one full interval before first background sync
        // so startup does not compete with user-initiated syncs
        await Task.Delay(
            TimeSpan.FromMinutes(settings.SyncIntervalMinutes),
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunSyncAsync(stoppingToken);
            await Task.Delay(
                TimeSpan.FromMinutes(settings.SyncIntervalMinutes),
                stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var projectRepo = scope.ServiceProvider.GetRequiredService<IScrivenerProjectRepository>();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

            var projects = await projectRepo.GetAllAsync(ct);

            foreach (var project in projects)
            {
                // Skip projects already being synced manually
                if (project.SyncStatus == SyncStatus.Syncing)
                {
                    logger.LogDebug(
                        "Skipping {ProjectName} - already syncing.", project.Name);
                    continue;
                }

                logger.LogDebug("Background syncing {ProjectName}...", project.Name);
                await syncService.ParseProjectAsync(project.Id, ct);
                await syncService.DetectContentChangesAsync(project.Id, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sync background service encountered an error.");
        }
    }
}
