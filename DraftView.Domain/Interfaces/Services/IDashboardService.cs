using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;

namespace DraftView.Domain.Interfaces.Services;

public interface IDashboardService
{
    Task<IReadOnlyList<Section>> GetProjectOverviewAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetReaderSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmailDeliveryLog>> GetEmailHealthSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AuthorNotification>> GetNotificationsAsync(
        Guid authorId, NotificationEventType? typeFilter = null, CancellationToken ct = default);

    Task DismissNotificationAsync(
        Guid notificationId, CancellationToken ct = default);

    Task DismissAllNotificationsAsync(
        Guid authorId, CancellationToken ct = default);

    Task DismissNotificationsByTypeAsync(
        Guid authorId, NotificationEventType? type, CancellationToken ct = default);
}
