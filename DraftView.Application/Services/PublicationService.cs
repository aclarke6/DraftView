using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class PublicationService(
    ISectionRepository sectionRepo,
    IUnitOfWork unitOfWork) : IPublicationService
{
    private static readonly IReadOnlySet<string> PublishableStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "First Draft", "Revised Draft", "Final Draft", "Done"
        };

    public async Task PublishChapterAsync(
        Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        var chapter = await sectionRepo.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        if (chapter.NodeType != NodeType.Folder)
            throw new InvariantViolationException("I-04",
                "Only folder sections (chapters) can be published via bulk publish.");

        var descendants = await sectionRepo.GetAllDescendantsAsync(chapterId, ct);
        var scenes      = descendants.Where(s => s.NodeType == NodeType.Document && !s.IsSoftDeleted).ToList();

        // Validate all scenes are publishable
        var notReady = scenes
            .Where(s => !string.IsNullOrWhiteSpace(s.ScrivenerStatus) &&
                        !PublishableStatuses.Contains(s.ScrivenerStatus))
            .ToList();

        if (notReady.Any())
        {
            var titles = string.Join(", ", notReady.Select(s => s.Title));
            throw new InvariantViolationException("I-CHAPTER-PUBLISH",
                $"Cannot publish chapter - the following scenes are not ready: {titles}");
        }

        // Mark the chapter folder as a published container
        chapter.MarkAsPublishedContainer();

        // Publish all scenes
        foreach (var scene in scenes)
            scene.PublishAsPartOfChapter(scene.ContentHash ?? string.Empty);

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnpublishChapterAsync(
        Guid chapterId, Guid authorId, CancellationToken ct = default)
    {
        var chapter = await sectionRepo.GetByIdAsync(chapterId, ct)
            ?? throw new EntityNotFoundException(nameof(Section), chapterId);

        chapter.UnmarkAsPublishedContainer();

        var descendants = await sectionRepo.GetAllDescendantsAsync(chapterId, ct);
        foreach (var scene in descendants.Where(s => s.NodeType == NodeType.Document))
            scene.Unpublish();

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Section>> GetPublishedChaptersAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var all = await sectionRepo.GetByProjectIdAsync(projectId, ct);
        return [.. all
            .Where(s => s.NodeType == NodeType.Folder && s.IsPublished && !s.IsSoftDeleted)
            .OrderBy(s => s.SortOrder)];
    }

    public async Task<IReadOnlyList<Section>> GetPublishableChaptersAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var all = await sectionRepo.GetByProjectIdAsync(projectId, ct);
        var result = new List<Section>();

        var folders = all.Where(s => s.NodeType == NodeType.Folder && !s.IsSoftDeleted);

        foreach (var folder in folders)
        {
            if (await CanPublishAsync(folder.Id, ct))
                result.Add(folder);
        }

        return result;
    }

    public async Task<bool> CanPublishAsync(Guid folderId, CancellationToken ct = default)
    {
        var descendants = await sectionRepo.GetAllDescendantsAsync(folderId, ct);
        var scenes      = descendants.Where(s => s.NodeType == NodeType.Document && !s.IsSoftDeleted).ToList();

        if (!scenes.Any()) return false;

        return scenes.All(s =>
            string.IsNullOrWhiteSpace(s.ScrivenerStatus) ||
            PublishableStatuses.Contains(s.ScrivenerStatus));
    }
}


