namespace DraftView.Domain.Notifications;

/// <summary>
/// Semantic filter groups for the Recent Activity panel.
/// Each group maps to one or more <see cref="NotificationEventType"/> values.
/// </summary>
public enum NotificationFilterGroup
{
    Comments,
    Replies,
    Readers,
    Sync
}
