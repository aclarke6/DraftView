namespace DraftView.Domain.Interfaces.Services;

public sealed class SyncProgress
{
    public int SectionsProcessed { get; set; }
    public string? CurrentSection { get; set; }
    public DateTime StartedAt { get; set; }
}

public interface ISyncProgressTracker
{
    void Start(Guid projectId);
    void Increment(Guid projectId, string sectionTitle);
    SyncProgress? Get(Guid projectId);
    void Clear(Guid projectId);
}
