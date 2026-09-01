namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Sends email notifications to readers whose snapshot-based change state
/// meets their notification threshold after a sync content update.
/// </summary>
public interface IChangeNotificationService
{
    /// <summary>
    /// For each reader who has read the section, computes their change state
    /// and sends a SectionChangedNotification email if the classification
    /// meets the reader's ReadingStyle threshold and NotifyOnSectionChanged is enabled.
    /// No-op for readers with no snapshot (New) or up-to-date snapshots.
    /// </summary>
    Task SendChangeNotificationsAsync(Guid sectionId, CancellationToken ct = default);
}
