using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class SystemStateMessageService(
    ISystemStateMessageRepository messageRepo,
    IUnitOfWork unitOfWork,
    IAuthorizationFacade authFacade) : ISystemStateMessageService
{
    public async Task<SystemStateMessage> CreateMessageAsync(string message, CancellationToken ct = default)
    {
        if (!authFacade.IsSystemSupport())
            throw new UnauthorisedOperationException("Only SystemSupport may create system state messages.");

        var active = await messageRepo.GetActiveAsync(ct);
        active?.Deactivate();

        var newMessage = SystemStateMessage.Create(message, Guid.Empty);
        await messageRepo.AddAsync(newMessage, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return newMessage;
    }

    public async Task DeactivateMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        if (!authFacade.IsSystemSupport())
            throw new UnauthorisedOperationException("Only SystemSupport may deactivate system state messages.");

        var all = await messageRepo.GetAllAsync(ct);
        var msg = all.FirstOrDefault(m => m.Id == messageId)
            ?? throw new EntityNotFoundException(nameof(SystemStateMessage), messageId);

        msg.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<SystemStateMessage?> GetActiveMessageAsync(CancellationToken ct = default) =>
        messageRepo.GetActiveAsync(ct);

    public async Task<IReadOnlyList<SystemStateMessage>> GetAllMessagesAsync(CancellationToken ct = default)
    {
        if (!authFacade.IsSystemSupport())
            throw new UnauthorisedOperationException("Only SystemSupport may view all system state messages.");

        return await messageRepo.GetAllAsync(ct);
    }
}
