using DraftView.Domain.Entities;
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
    ISyncOrchestrationService syncOrchestrationService) : IProjectManagementService
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
        var addedProjectIds = new List<Guid>();

        foreach (var d in toAdd)
        {
            try
            {
                var softDeleted = await projectRepo.GetSoftDeletedBySyncRootIdAsync(d.SyncRootId, ct);
                if (softDeleted is not null)
                {
                    softDeleted.Restore(d.Name);
                    addedProjectIds.Add(softDeleted.Id);
                    addedCount++;
                    singleAddedProjectName = d.Name;
                }
                else
                {
                    var project = Project.Create(d.Name, d.DropboxPath, authorId, d.SyncRootId);
                    await projectRepo.AddAsync(project, ct);
                    addedProjectIds.Add(project.Id);
                    addedCount++;
                    singleAddedProjectName = d.Name;
                }
            }
            catch (DuplicateProjectException) { }
        }

        await unitOfWork.SaveChangesAsync(ct);

        foreach (var projectId in addedProjectIds)
            await syncOrchestrationService.StartSyncAsync(projectId, ct);

        return new AddDiscoveredProjectsResultDto
        {
            AddedCount = addedCount,
            SingleAddedProjectName = addedCount == 1 ? singleAddedProjectName : null
        };
    }
}
