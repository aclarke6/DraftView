using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class ReadEventRepository(DraftViewDbContext db) : IReadEventRepository
{
    public async Task<ReadEvent?> GetAsync(Guid sectionId, Guid userId, CancellationToken ct = default) =>
        await db.ReadEvents.FirstOrDefaultAsync(
            r => r.SectionId == sectionId && r.UserId == userId, ct);

    public async Task<IReadOnlyList<ReadEvent>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await db.ReadEvents.Where(r => r.UserId == userId).ToListAsync(ct);

    public async Task<IReadOnlyList<ReadEvent>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.ReadEvents.Where(r => r.SectionId == sectionId).ToListAsync(ct);

    public async Task<IReadOnlyList<ReadEvent>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default) =>
        await db.ReadEvents
            .Where(r => db.Sections.Any(s => s.Id == r.SectionId && s.ProjectId == projectId))
            .ToListAsync(ct);

    public async Task<bool> HasReadAsync(Guid sectionId, Guid userId, CancellationToken ct = default) =>
        await db.ReadEvents.AnyAsync(r => r.SectionId == sectionId && r.UserId == userId, ct);

    public async Task AddAsync(ReadEvent readEvent, CancellationToken ct = default) =>
        await db.ReadEvents.AddAsync(readEvent, ct);
}
