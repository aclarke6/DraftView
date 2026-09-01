using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Builds the chapter and document data for the Publishing page.
/// </summary>
public class ContentNavigationService(
    ISectionRepository sectionRepository) : IContentNavigationService
{
    public async Task<IReadOnlyList<PublishingChapterData>> BuildPublishingChapterDataAsync(
        Guid projectId, ProjectType projectType, CancellationToken ct = default)
    {
        var sections = await sectionRepository.GetByProjectIdAsync(projectId, ct);
        var sorted = SortDepthFirst(sections);

        var chapters = new List<PublishingChapterData>();
        foreach (var chapter in GetPublishedLeafChapters(sorted))
            chapters.Add(BuildChapterData(chapter, sorted, projectType));

        return chapters;
    }

    private static PublishingChapterData BuildChapterData(
        Section chapter,
        IReadOnlyList<(Section Section, int Depth)> sorted,
        ProjectType projectType)
    {
        var documents = GetPublishedDocumentsForChapter(chapter.Id, sorted);
        var docData   = documents.Select(BuildDocumentData).ToList();

        var chapterHasChanges = documents.Any(d => d.ContentChangedSincePublish);

        return new PublishingChapterData(
            Chapter:              chapter,
            HasChanges:           chapterHasChanges,
            Classification:       null,
            CanRevoke:            false,
            ShowDocumentControls: documents.Count > 1 || projectType == ProjectType.Manual,
            Documents:            docData);
    }

    private static PublishingDocumentData BuildDocumentData(Section document) =>
        new(
            Document:           document,
            CurrentVersionNumber: null,
            CurrentVersionLabel:  null,
            HasChanges:           document.ContentChangedSincePublish,
            Classification:       null,
            CanRevoke:            false,
            VersionHistory:       [],
            ShowVersionHistory:   false,
            RetentionLimit:       0);

    private static IReadOnlyList<Section> GetPublishedLeafChapters(
        IReadOnlyList<(Section Section, int Depth)> sorted)
    {
        var folderChildIds = sorted
            .Where(x => x.Section.NodeType == NodeType.Folder && x.Section.ParentId.HasValue)
            .Select(x => x.Section.ParentId!.Value)
            .ToHashSet();

        return sorted
            .Select(x => x.Section)
            .Where(s => s.NodeType == NodeType.Folder &&
                        s.IsPublished &&
                        !folderChildIds.Contains(s.Id))
            .ToList();
    }

    private static IReadOnlyList<Section> GetPublishedDocumentsForChapter(
        Guid chapterId,
        IReadOnlyList<(Section Section, int Depth)> sorted) =>
        sorted
            .Select(x => x.Section)
            .Where(s => s.ParentId == chapterId &&
                        s.NodeType == NodeType.Document &&
                        !s.IsSoftDeleted)
            .ToList();

    private static IReadOnlyList<(Section Section, int Depth)> SortDepthFirst(
        IReadOnlyList<Section> sections)
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

        var result = new List<(Section, int)>();
        void Walk(Guid parentId, int depth)
        {
            if (!lookup.TryGetValue(parentId, out var children)) return;
            foreach (var child in children)
            {
                result.Add((child, depth));
                Walk(child.Id, depth + 1);
            }
        }
        Walk(root, 0);
        return result;
    }
}
