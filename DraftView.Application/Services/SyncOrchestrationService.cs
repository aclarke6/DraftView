using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Implements the sync kick-off workflow: persists the syncing status for a
/// project, then fires a scoped background Task that invokes ISyncService.
/// The background task handles its own error recording if the parse fails.
/// </summary>
public class SyncOrchestrationService(
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    ILogger<SyncOrchestrationService> logger) : ISyncOrchestrationService
{
    /// <summary>
    /// Fetches the project, marks it as syncing, saves the change, then
    /// enqueues a background Task.Run to parse the project. Returns once
    /// the status is persisted; the background parse runs asynchronously.
    /// Does nothing if the project is not found.
    /// </summary>
    public async Task StartSyncAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct);
        if (project is not null)
        {
            project.MarkSyncing();
            await unitOfWork.SaveChangesAsync(ct);
        }

        FireBackgroundSync(projectId);
    }

    /// <summary>
    /// Fires a fire-and-forget Task.Run that parses the project in an isolated
    /// DI scope. On failure, records the error on the project's SyncStatus.
    /// </summary>
    private void FireBackgroundSync(Guid projectId)
    {
        _ = Task.Run(async () =>
        {
            using var scope       = scopeFactory.CreateScope();
            var bgSyncService     = scope.ServiceProvider.GetRequiredService<ISyncService>();
            var bgProjectRepo     = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var bgUnitOfWork      = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            try
            {
                await bgSyncService.ParseProjectAsync(projectId);
                logger.LogInformation("Background sync completed for project {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background sync failed for project {ProjectId}: {Message}",
                    projectId, ex.Message);
                try
                {
                    var failedProject = await bgProjectRepo.GetByIdAsync(projectId);
                    if (failedProject is not null && failedProject.SyncStatus == SyncStatus.Syncing)
                    {
                        failedProject.UpdateSyncStatus(SyncStatus.Error, DateTime.UtcNow,
                            ex.Message.Length > 200 ? ex.Message[..200] : ex.Message);
                        await bgUnitOfWork.SaveChangesAsync();
                    }
                }
                catch (Exception innerEx)
                {
                    logger.LogError(innerEx,
                        "Failed to update sync error status for {ProjectId}", projectId);
                }
            }
        });
    }
}
