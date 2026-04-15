using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Infrastructure.Persistence;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class InvitationRepository(DraftViewDbContext db) : IInvitationRepository
{
    public async Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        await db.Invitations.FirstOrDefaultAsync(i => i.Token == token, ct);

    public async Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Invitations.FindAsync([id], ct);

    public async Task<Invitation?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await db.Invitations.FirstOrDefaultAsync(i => i.UserId == userId, ct);

    public async Task<IReadOnlyList<Invitation>> GetPendingByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await db.Invitations
            .Where(i => i.UserId == userId && i.Status == DraftView.Domain.Enumerations.InvitationStatus.Pending)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Invitation invitation, CancellationToken ct = default) =>
        await db.Invitations.AddAsync(invitation, ct);
}
