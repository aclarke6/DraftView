using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class SectionRepository(DraftViewDbContext db) : ISectionRepository
{
    public async Task<Section?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Sections.FindAsync([id], ct);

    public async Task<Section?> GetByScrivenerUuidAsync(Guid projectId, string uuid, CancellationToken ct = default) =>
        await db.Sections.FirstOrDefaultAsync(
            s => s.ProjectId == projectId && s.ScrivenerUuid == uuid, ct);

    public async Task<IReadOnlyList<Section>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Sections
            .Where(s => s.ProjectId == projectId && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Section>> GetPublishedByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        await db.Sections
            .Where(s => s.ProjectId == projectId && s.IsPublished && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Section>> GetChildrenAsync(Guid parentId, CancellationToken ct = default) =>
        await db.Sections
            .Where(s => s.ParentId == parentId && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Section>> GetAllDescendantsAsync(Guid parentId, CancellationToken ct = default)
    {
        // Recursive fetch using iterative BFS
        var result = new List<Section>();
        var queue = new Queue<Guid>();
        queue.Enqueue(parentId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = await db.Sections
                .Where(s => s.ParentId == currentId && !s.IsSoftDeleted)
                .ToListAsync(ct);

            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return result;
    }

    public async Task AddAsync(Section section, CancellationToken ct = default) =>
        await db.Sections.AddAsync(section, ct);
}
