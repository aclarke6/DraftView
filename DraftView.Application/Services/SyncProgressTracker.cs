using System.Collections.Concurrent;
using DraftView.Domain.Interfaces.Services;
namespace DraftView.Application.Services;
public class SyncProgressTracker : ISyncProgressTracker
{
    private readonly ConcurrentDictionary<Guid, SyncProgress> _progress = new();
    public void Start(Guid projectId)
    {
        _progress[projectId] = new SyncProgress
        {
            SectionsProcessed = 0,
            CurrentSection    = null,
            FilesDownloaded   = 0,
            TotalFiles        = 0,
            StartedAt         = DateTime.UtcNow
        };
    }
    public void Increment(Guid projectId, string sectionTitle)
    {
        _progress.AddOrUpdate(projectId,
            new SyncProgress { SectionsProcessed = 1, CurrentSection = sectionTitle, StartedAt = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.SectionsProcessed++;
                existing.CurrentSection = sectionTitle;
                return existing;
            });
    }
    public void IncrementFileDownloaded(Guid projectId)
    {
        _progress.AddOrUpdate(projectId,
            new SyncProgress { FilesDownloaded = 1, StartedAt = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.FilesDownloaded++;
                return existing;
            });
    }
    public void SetTotalFiles(Guid projectId, int total)
    {
        _progress.AddOrUpdate(projectId,
            new SyncProgress { TotalFiles = total, StartedAt = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.TotalFiles = total;
                return existing;
            });
    }
    public SyncProgress? Get(Guid projectId) =>
        _progress.TryGetValue(projectId, out var p) ? p : null;
    public void Clear(Guid projectId) =>
        _progress.TryRemove(projectId, out _);
}
