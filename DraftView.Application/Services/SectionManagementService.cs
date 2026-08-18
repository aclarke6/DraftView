using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Builds the aggregated sections summary for the author's Sections page,
/// encapsulating depth-first tree ordering, publishability evaluation, and
/// change classification across all project chapters.
/// </summary>
public class SectionManagementService(
    IProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    ISectionVersionRepository sectionVersionRepo,
    IPublicationService publicationService,
    IHtmlDiffService htmlDiffService,
    IChangeClassificationService changeClassificationService) : ISectionManagementService
{
    /// <summary>
    /// Loads the project and its full section tree, evaluates publishability
    /// for every folder, and computes change classifications for published
    /// chapters that have unpublished document edits. Returns null when the
    /// project does not exist.
    /// </summary>
    public async Task<SectionsSummaryDto?> GetSectionsSummaryAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct);
        if (project is null) return null;

        var sections = await sectionRepo.GetByProjectIdAsync(projectId, ct);
        var sorted   = SortDepthFirst(sections);

        var publishable = new HashSet<Guid>();
        foreach (var row in sorted.Where(x => x.Section.NodeType == NodeType.Folder))
        {
            if (await publicationService.CanPublishAsync(row.Section.Id, ct))
                publishable.Add(row.Section.Id);
        }

        var classificationMap = new Dictionary<Guid, ChangeClassification>();
        var chapterHasChanges = new HashSet<Guid>();
        foreach (var row in sorted.Where(x =>
                     x.Section.NodeType == NodeType.Folder &&
                     x.Section.IsPublished))
        {
            var chapter = row.Section;
            try
            {
                var documents = sorted
                    .Where(x => x.Section.ParentId == chapter.Id &&
                                x.Section.NodeType == NodeType.Document &&
                                !x.Section.IsSoftDeleted)
                    .Select(x => x.Section)
                    .ToList();

                if (!documents.Any(d => d.ContentChangedSincePublish))
                    continue;

                chapterHasChanges.Add(chapter.Id);

                var highestClassification = ChangeClassification.Polish;
                var hasClassifiableVersion = false;

                foreach (var document in documents)
                {
                    var latestVersion = await sectionVersionRepo.GetLatestAsync(document.Id, ct);
                    if (latestVersion is null) continue;

                    hasClassifiableVersion = true;
                    var diff = htmlDiffService.Compute(
                        latestVersion.HtmlContent,
                        document.HtmlContent ?? string.Empty);

                    var classification = changeClassificationService.Classify(diff);
                    if (classification.HasValue && classification.Value > highestClassification)
                        highestClassification = classification.Value;
                }

                if (hasClassifiableVersion)
                    classificationMap[chapter.Id] = highestClassification;
            }
            catch
            {
                // Classification indicator is advisory only; skip failures silently.
            }
        }

        return new SectionsSummaryDto
        {
            Project           = project,
            SortedSections    = sorted,
            Publishable       = publishable,
            ChapterHasChanges = chapterHasChanges,
            ClassificationMap = classificationMap
        };
    }

    /// <summary>
    /// Returns all sections in depth-first order, each paired with its
    /// indentation depth. Children are sorted by SortOrder within their parent.
    /// </summary>
    private static IReadOnlyList<SectionTreeRow> SortDepthFirst(
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

        var result = new List<SectionTreeRow>();

        void Walk(Guid parentId, int depth)
        {
            if (!lookup.TryGetValue(parentId, out var children)) return;
            foreach (var child in children)
            {
                result.Add(new SectionTreeRow(child, depth));
                Walk(child.Id, depth + 1);
            }
        }

        Walk(root, 0);
        return result;
    }
}
