using ScrivenerSync.Domain.Entities;
using ScrivenerSync.Domain.Exceptions;
using ScrivenerSync.Domain.Interfaces.Repositories;
using ScrivenerSync.Domain.Interfaces.Services;

namespace ScrivenerSync.Application.Services;

public class PublicationService(
    ISectionRepository sectionRepo,
    IScrivenerProjectRepository projectRepo,
    IUnitOfWork unitOfWork) : IPublicationService
{
    public async Task PublishAsync(Guid sectionId, Guid authorId, CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        section.Publish(section.ContentHash ?? string.Empty);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnpublishAsync(Guid sectionId, Guid authorId, CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), sectionId);

        section.Unpublish();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Section>> GetPublishedSectionsAsync(
        Guid projectId, CancellationToken ct = default) =>
        await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

    public async Task<IReadOnlyList<Section>> GetPublishableSectionsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var all = await sectionRepo.GetByProjectIdAsync(projectId, ct);
        return all.Where(s => s.NodeType == Domain.Enumerations.NodeType.Document
                           && !s.IsSoftDeleted).ToList();
    }
}
