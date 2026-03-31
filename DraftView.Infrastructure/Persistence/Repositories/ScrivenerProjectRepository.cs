using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class ScrivenerProjectRepository(DraftViewDbContext db) : IScrivenerProjectRepository
{
    public async Task<ScrivenerProject?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<ScrivenerProject>> GetAllAsync(CancellationToken ct = default) =>
        await db.Projects
            .Where(p => !p.IsSoftDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<ScrivenerProject?> GetReaderActiveProjectAsync(CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(p => p.IsReaderActive && !p.IsSoftDeleted, ct);

    public async Task<ScrivenerProject?> GetByScrivenerRootUuidAsync(
        string uuid, CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(
            p => p.ScrivenerRootUuid == uuid && !p.IsSoftDeleted, ct);

    public async Task<ScrivenerProject?> GetSoftDeletedByScrivenerRootUuidAsync(
        string uuid, CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(
            p => p.ScrivenerRootUuid == uuid && p.IsSoftDeleted, ct);

    public async Task AddAsync(ScrivenerProject project, CancellationToken ct = default)
    {
        if (project.ScrivenerRootUuid is not null)
        {
            var exists = await db.Projects.AnyAsync(
                p => p.ScrivenerRootUuid == project.ScrivenerRootUuid && !p.IsSoftDeleted, ct);
            if (exists)
                throw new DuplicateProjectException(project.ScrivenerRootUuid);
        }
        await db.Projects.AddAsync(project, ct);
    }

    public async Task<IReadOnlyList<(ScrivenerProject Project, DateTime SyncedAt)>> GetRecentlySyncedAsync(
        int take,
        CancellationToken ct = default)
    {
        var projects = await db.Projects
            .Where(p => !p.IsSoftDeleted
                     && p.LastSyncedAt != null
                     && p.SyncStatus == SyncStatus.Healthy)
            .OrderByDescending(p => p.LastSyncedAt)
            .Take(take)
            .ToListAsync(ct);

        return projects
            .Select(p => (p, p.LastSyncedAt!.Value))
            .ToList();
    }
}
