using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface ISystemStateMessageService
{
    Task<SystemStateMessage> CreateMessageAsync(string message, CancellationToken ct = default);
    Task DeactivateMessageAsync(Guid messageId, CancellationToken ct = default);
    Task<SystemStateMessage?> GetActiveMessageAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SystemStateMessage>> GetAllMessagesAsync(CancellationToken ct = default);
}
