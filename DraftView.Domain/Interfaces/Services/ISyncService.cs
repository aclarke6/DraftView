namespace DraftView.Domain.Interfaces.Services;

public interface ISyncService
{
    Task ParseProjectAsync(Guid projectId, CancellationToken ct = default);
    Task DetectContentChangesAsync(Guid projectId, CancellationToken ct = default);
}
