using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IProjectService
{
    Task ActivateForReadersAsync(Guid projectId, Guid authorId, CancellationToken ct = default);
    Task DeactivateForReadersAsync(Guid projectId, Guid authorId, CancellationToken ct = default);
    Task<ScrivenerProject?> GetReaderActiveProjectAsync(CancellationToken ct = default);
    Task<ScrivenerProject> CreateProjectAsync(string name, string dropboxPath, Guid authorId, CancellationToken ct = default);
}
