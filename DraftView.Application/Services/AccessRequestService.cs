using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

public class AccessRequestService(
    IAccessRequestRepository accessRequestRepo,
    IProjectRepository projectRepo,
    IUserRepository userRepo,
    IReaderAccessRepository readerAccessRepo,
    IAuthorNotificationRepository notificationRepo,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender) : IAccessRequestService
{
    public async Task SubmitRequestAsync(Guid readerId, Guid projectId, string? coverNote, string? contactEmail, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new EntityNotFoundException("Project", projectId);

        if (!project.IsOpen)
            throw new InvariantViolationException("I-AR-CLOSED",
                "The project is not open for access requests.");

        var pending = await accessRequestRepo.GetPendingByProjectIdAsync(projectId, ct);
        if (pending.Any(r => r.ReaderId == readerId))
            throw new InvariantViolationException("I-AR-DUP",
                "A pending access request already exists for this reader and project.");

        var request = AccessRequest.Create(readerId, projectId, coverNote, contactEmail);
        await accessRequestRepo.AddAsync(request, ct);

        var author = await userRepo.GetAuthorAsync(ct);
        if (author is not null)
        {
            var notification = AuthorNotification.Create(
                author.Id,
                NotificationEventType.AccessRequest,
                "New access request",
                null,
                null,
                DateTime.UtcNow);
            await notificationRepo.AddAsync(notification, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ApproveRequestAsync(Guid requestId, Guid authorId, CancellationToken ct = default)
    {
        var request = await accessRequestRepo.GetByIdAsync(requestId, ct)
            ?? throw new EntityNotFoundException("AccessRequest", requestId);

        var project = await projectRepo.GetByIdAsync(request.ProjectId, ct)
            ?? throw new EntityNotFoundException("Project", request.ProjectId);

        if (project.AuthorId != authorId)
            throw new UnauthorisedOperationException(
                "Only the project author can approve access requests.");

        var reader = await userRepo.GetByIdAsync(request.ReaderId, ct)
            ?? throw new EntityNotFoundException("User", request.ReaderId);

        var existing = await readerAccessRepo.GetByReaderAndProjectAsync(request.ReaderId, request.ProjectId, ct);
        if (existing is null)
        {
            var access = ReaderAccess.Grant(request.ReaderId, authorId, request.ProjectId);
            await readerAccessRepo.AddAsync(access, ct);
        }

        request.Approve();

        await emailSender.SendAsync(
            reader.Email,
            reader.DisplayName,
            $"Your request to read \"{project.Name}\" has been approved",
            $"<p>Great news! Your request to read <strong>{project.Name}</strong> has been approved. You can now access the manuscript.</p>",
            ct);

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeclineRequestAsync(Guid requestId, Guid authorId, CancellationToken ct = default)
    {
        var request = await accessRequestRepo.GetByIdAsync(requestId, ct)
            ?? throw new EntityNotFoundException("AccessRequest", requestId);

        var project = await projectRepo.GetByIdAsync(request.ProjectId, ct)
            ?? throw new EntityNotFoundException("Project", request.ProjectId);

        if (project.AuthorId != authorId)
            throw new UnauthorisedOperationException(
                "Only the project author can decline access requests.");

        request.Decline();

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task BulkDeclineOnRevokeAsync(Guid projectId, CancellationToken ct = default)
    {
        await accessRequestRepo.BulkDeclineByProjectAsync(projectId, DateTime.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
