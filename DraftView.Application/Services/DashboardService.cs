using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

public class DashboardService(
    ISectionRepository sectionRepo,
    IUserRepository userRepo,
    IEmailDeliveryLogRepository logRepo,
    IAuthorNotificationRepository notificationRepo,
    IUnitOfWork unitOfWork) : IDashboardService
{
    public async Task<IReadOnlyList<Section>> GetProjectOverviewAsync(
        Guid projectId, CancellationToken ct = default) =>
        await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

    public async Task<IReadOnlyList<User>> GetReaderSummaryAsync(
        CancellationToken ct = default) =>
        await userRepo.GetAllBetaReadersAsync(ct);

    public async Task<IReadOnlyList<EmailDeliveryLog>> GetEmailHealthSummaryAsync(
        CancellationToken ct = default) =>
        await logRepo.GetFailedAsync(ct);

    /// <summary>
    /// Returns notifications for the author, pruning any older than 90 days first.
    /// When typeFilter is provided, only notifications of that event type are returned.
    /// </summary>
    public async Task<IReadOnlyList<AuthorNotification>> GetNotificationsAsync(
        Guid authorId, NotificationEventType? typeFilter = null, CancellationToken ct = default)
    {
        await notificationRepo.PruneOlderThanAsync(authorId, DateTime.UtcNow.AddDays(-90), ct);
        return typeFilter.HasValue
            ? await notificationRepo.GetByAuthorIdAndTypeAsync(authorId, typeFilter.Value, ct)
            : await notificationRepo.GetByAuthorIdAsync(authorId, ct);
    }

    public async Task DismissNotificationAsync(
        Guid notificationId, CancellationToken ct = default)
    {
        await notificationRepo.DeleteAsync(notificationId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DismissAllNotificationsAsync(
        Guid authorId, CancellationToken ct = default)
    {
        await notificationRepo.DeleteAllByAuthorIdAsync(authorId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes notifications for the author scoped by type.
    /// When type is null, all notifications for the author are deleted.
    /// </summary>
    public async Task DismissNotificationsByTypeAsync(
        Guid authorId, NotificationEventType? type, CancellationToken ct = default)
    {
        if (type.HasValue)
            await notificationRepo.DeleteByAuthorIdAndTypeAsync(authorId, type.Value, ct);
        else
            await notificationRepo.DeleteAllByAuthorIdAsync(authorId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
