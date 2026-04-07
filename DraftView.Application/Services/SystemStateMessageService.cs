using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class SystemStateMessageService(
    ISystemStateMessageRepository messageRepo,
    IUserRepository userRepo,
    IUnitOfWork unitOfWork,
    IAuthorizationFacade authFacade) : ISystemStateMessageService
{
    public async Task<SystemStateMessage> CreateMessageAsync(
        string message,
        SystemStateMessageSeverity severity = SystemStateMessageSeverity.Info,
        CancellationToken ct = default)
    {
        if (!authFacade.IsSystemSupport())
            throw new UnauthorisedOperationException("Only SystemSupport may create system state messages.");

        var active = await messageRepo.GetActiveAsync(ct);
        active?.Deactivate();

        var email        = authFacade.GetCurrentUserEmail() ?? string.Empty;
        var currentUser  = await userRepo.GetByEmailAsync(email, ct);
        var createdById  = currentUser?.Id ?? Guid.Empty;

        var newMessage = SystemStateMessage.Create(message, createdById, severity);
        await messageRepo.AddAsync(newMessage, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return newMessage;
    }

    public async Task DeactivateMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        if (!authFacade.IsSystemSupport())
            throw new UnauthorisedOperationException("Only SystemSupport may deactivate system state messages.");

        var msg = await messageRepo.GetByIdAsync(messageId, ct)
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
