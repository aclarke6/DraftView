using Microsoft.EntityFrameworkCore;
using DraftView.Domain.Entities;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Notifications;

namespace DraftView.Infrastructure.Persistence.Repositories;

public class AuthorNotificationRepository(DraftViewDbContext db) : IAuthorNotificationRepository
{
    public async Task AddAsync(AuthorNotification notification, CancellationToken ct = default) =>
        await db.AuthorNotifications.AddAsync(notification, ct);

    public async Task<IReadOnlyList<AuthorNotification>> GetByAuthorIdAsync(
        Guid authorId, CancellationToken ct = default) =>
        await db.AuthorNotifications
            .Where(n => n.AuthorId == authorId)
            .OrderByDescending(n => n.OccurredAt)
            .ToListAsync(ct);

    /// <summary>
    /// Returns all notifications for the given author with the specified event type,
    /// ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<AuthorNotification>> GetByAuthorIdAndTypeAsync(
        Guid authorId, NotificationEventType type, CancellationToken ct = default) =>
        await db.AuthorNotifications
            .Where(n => n.AuthorId == authorId && n.EventType == type)
            .OrderByDescending(n => n.OccurredAt)
            .ToListAsync(ct);

    /// <summary>
    /// Returns all notifications for the given author whose event type is in the provided list,
    /// ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<AuthorNotification>> GetByAuthorIdAndTypesAsync(
        Guid authorId, IReadOnlyList<NotificationEventType> types, CancellationToken ct = default) =>
        await db.AuthorNotifications
            .Where(n => n.AuthorId == authorId && types.Contains(n.EventType))
            .OrderByDescending(n => n.OccurredAt)
            .ToListAsync(ct);

    public async Task DeleteAsync(Guid notificationId, CancellationToken ct = default)
    {
        var n = await db.AuthorNotifications.FindAsync([notificationId], ct);
        if (n is not null)
            db.AuthorNotifications.Remove(n);
    }

    public async Task DeleteAllByAuthorIdAsync(Guid authorId, CancellationToken ct = default)
    {
        var all = await db.AuthorNotifications
            .Where(n => n.AuthorId == authorId)
            .ToListAsync(ct);
        db.AuthorNotifications.RemoveRange(all);
    }

    /// <summary>
    /// Removes all notifications for the given author matching the specified event type.
    /// Does not call SaveChanges — the caller is responsible for the unit of work.
    /// </summary>
    public async Task DeleteByAuthorIdAndTypeAsync(
        Guid authorId, NotificationEventType type, CancellationToken ct = default)
    {
        var items = await db.AuthorNotifications
            .Where(n => n.AuthorId == authorId && n.EventType == type)
            .ToListAsync(ct);
        db.AuthorNotifications.RemoveRange(items);
    }

    public async Task PruneOlderThanAsync(Guid authorId, DateTime cutoff, CancellationToken ct = default)
    {
        var old = await db.AuthorNotifications
            .Where(n => n.AuthorId == authorId && n.OccurredAt < cutoff)
            .ToListAsync(ct);
        db.AuthorNotifications.RemoveRange(old);
    }
}
