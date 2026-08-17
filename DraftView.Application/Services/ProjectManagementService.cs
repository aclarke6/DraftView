using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ProjectManagementService(
    IProjectDiscoveryService discoveryService,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    ILogger<ProjectManagementService> logger) : IProjectManagementService
{
    public async Task<AddDiscoveredProjectsResultDto> AddDiscoveredProjectsAsync(
        IReadOnlyList<string> selectedUuids, Guid authorId, CancellationToken ct = default)
    {
        if (selectedUuids is null || selectedUuids.Count == 0)
            return new AddDiscoveredProjectsResultDto { AddedCount = 0 };

        var discovered = await discoveryService.DiscoverAsync(authorId, ct);
        var toAdd      = discovered
            .Where(d => selectedUuids.Contains(d.SyncRootId) && !d.AlreadyAdded)
            .ToList();

        var addedCount = 0;
        foreach (var d in toAdd)
        {
            try
            {
                var softDeleted = await projectRepo.GetSoftDeletedBySyncRootIdAsync(d.SyncRootId, ct);
                if (softDeleted is not null)
                {
                    softDeleted.Restore(d.Name);
                    addedCount++;
                }
                else
                {
                    var project = Project.Create(d.Name, d.DropboxPath, authorId, d.SyncRootId);
                    await projectRepo.AddAsync(project, ct);
                    addedCount++;
                }
            }
            catch (DuplicateProjectException) { }
        }

        await unitOfWork.SaveChangesAsync(ct);

        var projectsToSync = new List<Guid>();
        foreach (var d in toAdd)
        {
            var projects = await projectRepo.GetAllAsync(ct);
            var project  = projects.FirstOrDefault(p => p.SyncRootId == d.SyncRootId);
            if (project is null) continue;
            project.MarkSyncing();
            projectsToSync.Add(project.Id);
        }

        if (projectsToSync.Count > 0)
            await unitOfWork.SaveChangesAsync(ct);

        foreach (var projectId in projectsToSync)
            StartBackgroundSync(projectId);

        return new AddDiscoveredProjectsResultDto
        {
            AddedCount = addedCount,
            SingleAddedProjectName = addedCount == 1 ? toAdd.First().Name : null
        };
    }

    private void StartBackgroundSync(Guid projectId)
    {
        _ = Task.Run(async () =>
        {
            using var scope   = scopeFactory.CreateScope();
            var bgSyncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
            var bgProjectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var bgUnitOfWork  = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
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
                    logger.LogError(innerEx, "Failed to update sync error status for {ProjectId}", projectId);
                }
            }
        });
    }
}
