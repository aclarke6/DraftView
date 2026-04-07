using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class SystemStateMessageRepository(DraftViewDbContext db) : ISystemStateMessageRepository
{
    public Task<SystemStateMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.SystemStateMessages
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<SystemStateMessage?> GetActiveAsync(CancellationToken ct = default) =>
        db.SystemStateMessages
            .FirstOrDefaultAsync(m => m.IsActive, ct);

    public Task<IReadOnlyList<SystemStateMessage>> GetAllAsync(CancellationToken ct = default) =>
        db.SystemStateMessages
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<SystemStateMessage>)t.Result, ct);

    public async Task AddAsync(SystemStateMessage message, CancellationToken ct = default) =>
        await db.SystemStateMessages.AddAsync(message, ct);
}
