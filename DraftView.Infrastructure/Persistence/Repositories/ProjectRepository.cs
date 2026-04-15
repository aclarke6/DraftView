using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class ProjectRepository(DraftViewDbContext db) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default) =>
        await db.Projects
            .Where(p => !p.IsSoftDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<Project?> GetReaderActiveProjectAsync(CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(p => p.IsReaderActive && !p.IsSoftDeleted, ct);

    public async Task<Project?> GetBySyncRootIdAsync(
        string uuid, CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(
            p => p.SyncRootId == uuid && !p.IsSoftDeleted, ct);

    public async Task<Project?> GetSoftDeletedBySyncRootIdAsync(
        string uuid, CancellationToken ct = default) =>
        await db.Projects.FirstOrDefaultAsync(
            p => p.SyncRootId == uuid && p.IsSoftDeleted, ct);

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        if (project.SyncRootId is not null)
        {
            var exists = await db.Projects.AnyAsync(
                p => p.SyncRootId == project.SyncRootId && !p.IsSoftDeleted, ct);
            if (exists)
                throw new DuplicateProjectException(project.SyncRootId);
        }
        await db.Projects.AddAsync(project, ct);
    }
}
