using DraftView.Domain.Entities;
using DraftView.Domain.Notifications;

namespace DraftView.Domain.Interfaces.Services;

public interface IDashboardService
{
    Task<IReadOnlyList<Section>> GetProjectOverviewAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetReaderSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmailDeliveryLog>> GetEmailHealthSummaryAsync(CancellationToken ct = default);

    Task<IReadOnlyList<NotificationItemDto>> GetRecentNotificationsAsync(
        Guid authorUserId,
        int maxItems = 20,
        CancellationToken ct = default);
}
