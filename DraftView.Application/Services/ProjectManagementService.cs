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

    /// <summary>
    /// Makes the specified project active for readers, deactivating the current
    /// active project first if it differs.
    /// </summary>
    public async Task SetActiveProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var currentlyActive = await projectRepo.GetReaderActiveProjectAsync(ct);
        if (currentlyActive is not null && currentlyActive.Id != project.Id)
            currentlyActive.DeactivateForReaders();

        project.ActivateForReaders();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Deactivates the specified project for readers.</summary>
    public async Task DeactivateProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.DeactivateForReaders();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Soft-deletes the specified project.</summary>
    public async Task SoftDeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        project.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }
}
