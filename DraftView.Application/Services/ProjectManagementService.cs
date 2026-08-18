using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Orchestrates adding discovered Scrivener/Dropbox projects for an author:
/// restoring soft-deleted projects or creating new ones, then kicking off
/// background synchronisation for each newly added project.
/// </summary>
public class ProjectManagementService(
    IProjectDiscoveryService discoveryService,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    ILogger<ProjectManagementService> logger) : IProjectManagementService
{
    /// <summary>
    /// Matches the selected UUIDs against the author's discovered projects,
    /// restores any that were previously soft-deleted, or creates new Project
    /// entities. Saves to the database, then enqueues a background sync for
    /// each project that was added.
    /// </summary>
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
        string? singleAddedProjectName = null;
        foreach (var d in toAdd)
        {
            try
            {
                var softDeleted = await projectRepo.GetSoftDeletedBySyncRootIdAsync(d.SyncRootId, ct);
                if (softDeleted is not null)
                {
                    softDeleted.Restore(d.Name);
                    addedCount++;
                    singleAddedProjectName = d.Name;
                }
                else
                {
                    var project = Project.Create(d.Name, d.DropboxPath, authorId, d.SyncRootId);
                    await projectRepo.AddAsync(project, ct);
                    addedCount++;
                    singleAddedProjectName = d.Name;
                }
            }
            catch (DuplicateProjectException) { }
        }

        await unitOfWork.SaveChangesAsync(ct);

        var syncRootIdsToSync = toAdd.Select(d => d.SyncRootId).ToHashSet();
        var projectsToSync    = new List<Guid>();

        if (syncRootIdsToSync.Count > 0)
        {
            var allProjects = await projectRepo.GetAllAsync(ct);
            foreach (var project in allProjects.Where(p =>
                         p.SyncRootId is not null && syncRootIdsToSync.Contains(p.SyncRootId)))
            {
                project.MarkSyncing();
                projectsToSync.Add(project.Id);
            }
        }

        if (projectsToSync.Count > 0)
            await unitOfWork.SaveChangesAsync(ct);

        foreach (var projectId in projectsToSync)
            StartBackgroundSync(projectId);

        return new AddDiscoveredProjectsResultDto
        {
            AddedCount = addedCount,
            SingleAddedProjectName = addedCount == 1 ? singleAddedProjectName : null
        };
    }

    /// <summary>
    /// Fires a background Task.Run that parses the project via ISyncService
    /// in an isolated DI scope. On failure, attempts to record the error
    /// on the project's SyncStatus.
    /// </summary>
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
