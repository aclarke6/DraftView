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
                        !PublishableStatus.Contains(s.ScrivenerStatus))
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
        return [.. DepthFirstOrder(all)
            .Where(s => s.NodeType == NodeType.Folder && s.IsPublished && !s.IsSoftDeleted)];
    }

    private static IReadOnlyList<Section> DepthFirstOrder(IReadOnlyList<Section> sections)
    {
        var root   = Guid.Empty;
        var lookup = new Dictionary<Guid, List<Section>>();

        foreach (var s in sections)
        {
            var key = s.ParentId ?? root;
            if (!lookup.ContainsKey(key)) lookup[key] = [];
            lookup[key].Add(s);
        }

        foreach (var key in lookup.Keys.ToList())
            lookup[key] = [.. lookup[key].OrderBy(s => s.SortOrder)];

        var result = new List<Section>();

        void Walk(Guid parentId)
        {
            if (!lookup.TryGetValue(parentId, out var children)) return;
            foreach (var child in children)
            {
                result.Add(child);
                Walk(child.Id);
            }
        }

        Walk(root);
        return result;
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
            PublishableStatus.Contains(s.ScrivenerStatus));
    }
}


