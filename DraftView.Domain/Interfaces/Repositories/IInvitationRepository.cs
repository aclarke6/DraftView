using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface IInvitationRepository
{
    Task<Invitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invitation?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Invitation invitation, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent accepted invitations paired with their user,
    /// newest first. Used by the dashboard notifications panel.
    /// </summary>
    Task<IReadOnlyList<(Invitation Invitation, User User)>> GetRecentlyAcceptedAsync(
        int take,
        CancellationToken ct = default);
}

