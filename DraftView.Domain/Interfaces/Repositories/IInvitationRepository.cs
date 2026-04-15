using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IInvitationRepository
{
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invitation?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Invitation>> GetPendingByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Invitation invitation, CancellationToken ct = default);
}

