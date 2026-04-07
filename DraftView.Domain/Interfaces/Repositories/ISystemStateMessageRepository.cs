using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Repositories;

public interface ISystemStateMessageRepository
{
    /// <summary>Returns the message with the given id, or null if not found.</summary>
    Task<SystemStateMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the single active message, or null if none is active.</summary>
    Task<SystemStateMessage?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Returns all messages ordered by CreatedAt descending.</summary>
    Task<IReadOnlyList<SystemStateMessage>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Adds a new SystemStateMessage.</summary>
    Task AddAsync(SystemStateMessage message, CancellationToken ct = default);
}
