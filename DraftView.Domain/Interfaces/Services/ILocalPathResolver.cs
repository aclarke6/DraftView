using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface ILocalPathResolver
{
    /// <summary>Sets the user whose cache path should be used.</summary>
    void SetUserId(Guid userId);

    Task<string> ResolveAsync(Project project, CancellationToken ct = default);
    Task<string> ResolveScrivxAsync(Project project, CancellationToken ct = default);
}
