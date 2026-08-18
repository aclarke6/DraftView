using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Policies;
using Microsoft.Extensions.Configuration;

namespace DraftView.Application.Services;

/// <summary>
/// Builds the chapter and document data for the Publishing page by loading sections,
/// traversing the tree depth-first, and enriching each document with version history
/// and advisory change-classification metadata.
/// </summary>
public class ContentNavigationService(
    ISectionRepository sectionRepository,
    ISectionVersionRepository sectionVersionRepository,
    IHtmlDiffService htmlDiffService,
    IChangeClassificationService changeClassificationService,
    IConfiguration configuration) : IContentNavigationService
{
    /// <summary>
    /// Loads all sections for the project, identifies published leaf chapters, and returns
    /// their documents with version and classification data. Returns an empty list when no
    /// published leaf chapters exist.
    /// </summary>
    public async Task<IReadOnlyList<PublishingChapterData>> BuildPublishingChapterDataAsync(
        Guid projectId, ProjectType projectType, CancellationToken ct = default)
    {
        var sections = await sectionRepository.GetByProjectIdAsync(projectId, ct);
        var sorted = SortDepthFirst(sections);

        var chapters = new List<PublishingChapterData>();
        foreach (var chapter in GetPublishedLeafChapters(sorted))
            chapters.Add(await BuildChapterDataAsync(chapter, sorted, projectType, ct));

        return chapters;
    }

    /// <summary>
    /// Builds document data for each direct Document child of the chapter, then aggregates
    /// has-changes and highest classification across all documents in the chapter.
    /// </summary>
    private async Task<PublishingChapterData> BuildChapterDataAsync(
        Section chapter,
        IReadOnlyList<(Section Section, int Depth)> sorted,
        ProjectType projectType,
        CancellationToken ct)
    {
        var documents = GetPublishedDocumentsForChapter(chapter.Id, sorted);
        var docData = new List<PublishingDocumentData>();
        foreach (var doc in documents)
            docData.Add(await BuildDocumentDataAsync(doc, ct));

        var chapterHasChanges = documents.Any(d => d.ContentChangedSincePublish);
        var chapterClassification = docData
            .Where(d => d.Classification.HasValue)
            .Select(d => d.Classification)
            .Max();

        return new PublishingChapterData(
            Chapter: chapter,
            HasChanges: chapterHasChanges,
            Classification: chapterClassification,
            CanRevoke: docData.Any(d => d.CanRevoke),
            ShowDocumentControls: documents.Count > 1 || projectType == ProjectType.Manual,
            Documents: docData);
    }

    /// <summary>
    /// Retrieves the latest and all versions for a document section, computes retention
    /// state, and assembles version history when the retention limit is reached.
    /// </summary>
    private async Task<PublishingDocumentData> BuildDocumentDataAsync(Section document, CancellationToken ct)
    {
        var latestVersion = await sectionVersionRepository.GetLatestAsync(document.Id, ct);
        var allVersions = latestVersion is not null
            ? await sectionVersionRepository.GetAllBySectionIdAsync(document.Id, ct)
            : [];

        var tier = GetSubscriptionTier();
        var limit = VersionRetentionPolicy.GetLimit(tier);
        var atLimit = VersionRetentionPolicy.IsAtLimit(allVersions.Count, tier);
        var latestVersionNumber = allVersions.Any() ? allVersions.Max(v => v.VersionNumber) : 0;

        var versionHistory = atLimit
            ? allVersions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new VersionHistoryData(
                    VersionId: v.Id,
                    VersionNumber: v.VersionNumber,
                    VersionLabel: v.VersionLabel,
                    CreatedAt: v.CreatedAt,
                    Classification: v.ChangeClassification,
                    CanDelete: v.VersionNumber != latestVersionNumber))
                .ToList()
            : (IReadOnlyList<VersionHistoryData>)[];

        return new PublishingDocumentData(
            Document: document,
            CurrentVersionNumber: latestVersion?.VersionNumber,
            CurrentVersionLabel: latestVersion?.VersionLabel,
            HasChanges: document.ContentChangedSincePublish,
            Classification: TryClassifyDocumentChanges(document, latestVersion),
            CanRevoke: allVersions.Count > 1,
            VersionHistory: versionHistory,
            ShowVersionHistory: atLimit,
            RetentionLimit: limit);
    }

    private SubscriptionTier GetSubscriptionTier()
    {
        var raw = configuration["DraftView:SubscriptionTier"];
        return Enum.TryParse<SubscriptionTier>(raw, ignoreCase: true, out var tier)
            ? tier
            : SubscriptionTier.Free;
    }

    private ChangeClassification? TryClassifyDocumentChanges(Section document, SectionVersion? latestVersion)
    {
        if (latestVersion is null || !document.ContentChangedSincePublish)
            return null;

        try
        {
            var diff = htmlDiffService.Compute(latestVersion.HtmlContent, document.HtmlContent ?? string.Empty);
            return changeClassificationService.Classify(diff);
        }
        catch
        {
            return null;
        }
    }

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

    /// <summary>
    /// Returns all sections in depth-first order, each paired with its indentation depth.
    /// Children are sorted by SortOrder within their parent.
    /// </summary>
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
