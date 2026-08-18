namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates the two-step project sync kick-off: marking the project as
/// syncing in the database, then firing a background parse. Callers do not
/// await the background work — the method returns once the sync status is
/// persisted and the background task has been enqueued.
/// </summary>
public interface ISyncOrchestrationService
{
    /// <summary>
    /// Marks <paramref name="projectId"/> as syncing, persists the status,
    /// then fires a background parse via <see cref="ISyncService"/>. Returns
    /// immediately; the background work continues after the caller's request
    /// has completed.
    /// </summary>
    Task StartSyncAsync(Guid projectId, CancellationToken ct = default);
}
