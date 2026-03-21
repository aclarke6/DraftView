using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Application.Services;

public class ReadingProgressService(
    IReadEventRepository readEventRepo,
    ISectionRepository sectionRepo,
    IUnitOfWork unitOfWork) : IReadingProgressService
{
    public async Task RecordOpenAsync(
        Guid sectionId, Guid userId, CancellationToken ct = default)
    {
        var existing = await readEventRepo.GetAsync(sectionId, userId, ct);

        if (existing is null)
        {
            var readEvent = ReadEvent.Create(sectionId, userId);
            await readEventRepo.AddAsync(readEvent, ct);
        }
        else
        {
            existing.RecordOpen();
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<bool> IsCaughtUpAsync(
        Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var published = await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

        if (published.Count == 0)
            return true;

        foreach (var section in published)
        {
            if (!await readEventRepo.HasReadAsync(section.Id, userId, ct))
                return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<ReadEvent>> GetProgressForProjectAsync(
        Guid projectId, CancellationToken ct = default) =>
        await readEventRepo.GetByProjectIdAsync(projectId, ct);
}
