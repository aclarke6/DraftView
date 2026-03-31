using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
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

    public async Task AddAsync(Invitation invitation, CancellationToken ct = default) =>
        await db.Invitations.AddAsync(invitation, ct);

    public async Task<IReadOnlyList<(Invitation Invitation, User User)>> GetRecentlyAcceptedAsync(
        int take,
        CancellationToken ct = default)
    {
        var rows = await (
            from inv in db.Invitations
            join u in db.AppUsers on inv.UserId equals u.Id
            where inv.Status == InvitationStatus.Accepted
               && inv.AcceptedAt != null
            orderby inv.AcceptedAt descending
            select new { inv, u }
        )
        .Take(take)
        .ToListAsync(ct);

        return rows.Select(r => (r.inv, r.u)).ToList();
    }
}
