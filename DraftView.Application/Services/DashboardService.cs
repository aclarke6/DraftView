using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Notifications;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class DashboardService(
    ISectionRepository sectionRepo,
    IUserRepository userRepo,
    IEmailDeliveryLogRepository logRepo,
    ICommentRepository commentRepo,
    IInvitationRepository invitationRepo,
    IScrivenerProjectRepository projectRepo) : IDashboardService
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

    public async Task<IReadOnlyList<NotificationItemDto>> GetRecentNotificationsAsync(
        Guid authorUserId, int maxItems = 20, CancellationToken ct = default)
    {
        var poolSize    = maxItems * 3;
        var commentRows = await commentRepo.GetRecentCommentsForDashboardAsync(authorUserId, poolSize, ct);
        var readerJoins = await invitationRepo.GetRecentlyAcceptedAsync(poolSize, ct);
        var syncEvents  = await projectRepo.GetRecentlySyncedAsync(poolSize, ct);

        var items = new List<NotificationItemDto>(poolSize);

        foreach (CommentNotificationRow row in commentRows.Where(r => r.Status == CommentStatus.New))
        {
            if (row.ParentCommentAuthorId == authorUserId)
                items.Add(NotificationItemDto.ReplyToAuthor(
                    row.CommentAuthorName, row.SectionTitle,
                    row.Body, row.SectionId, row.CreatedAt));
            else if (row.ParentCommentId is null)
                items.Add(NotificationItemDto.NewComment(
                    row.CommentAuthorName, row.SectionTitle,
                    row.Body, row.SectionId, row.CreatedAt));
        }

        foreach ((Invitation invitation, User user) in readerJoins)
            if (invitation.AcceptedAt.HasValue)
                items.Add(NotificationItemDto.ReaderJoined(user.DisplayName, invitation.AcceptedAt.Value));

        foreach ((ScrivenerProject project, DateTime syncedAt) in syncEvents)
            items.Add(NotificationItemDto.SyncCompleted(project.Name, syncedAt));

        return items.OrderByDescending(i => i.OccurredAt).Take(maxItems).ToList();
    }
}
