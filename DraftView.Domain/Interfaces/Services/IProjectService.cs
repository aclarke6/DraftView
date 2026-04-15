using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IProjectService
{
    Task ActivateForReadersAsync(Guid projectId, Guid authorId, CancellationToken ct = default);
    Task DeactivateForReadersAsync(Guid projectId, Guid authorId, CancellationToken ct = default);
    Task<Project?> GetReaderActiveProjectAsync(CancellationToken ct = default);
    Task<Project> CreateProjectAsync(string name, string dropboxPath, Guid authorId, CancellationToken ct = default);
}
