using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

public interface IReadingProgressService
{
    Task RecordOpenAsync(Guid sectionId, Guid userId, CancellationToken ct = default);
    Task<bool> IsCaughtUpAsync(Guid userId, Guid projectId, CancellationToken ct = default);
    Task<bool> HasReadSectionAsync(Guid userId, Guid sectionId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadEvent>> GetProgressForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<ReadEvent?> GetLastReadEventAsync(Guid userId, Guid projectId, CancellationToken ct = default);
}

