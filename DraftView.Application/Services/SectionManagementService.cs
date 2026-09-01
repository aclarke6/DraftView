using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Builds the aggregated sections summary and section detail data for the author's
/// Sections and Section pages, encapsulating depth-first tree ordering, publishability
/// evaluation, change classification, and comment author resolution.
/// </summary>
public class SectionManagementService(
    IProjectRepository projectRepo,
    ISectionRepository sectionRepo,
    IPublicationService publicationService,
    ICommentService commentService,
    IUserRepository userRepository,
    IReadEventRepository readEventRepository,
    IReaderSnapshotRepository snapshotRepository) : ISectionManagementService
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
    /// Loads a section with its parent chapter title, all comment threads,
    /// a display-name map for every comment author, and the read count.
    /// Returns null when the section does not exist.
    /// </summary>
    public async Task<SectionDetailDto?> GetSectionDetailAsync(
        Guid sectionId, Guid authorId, CancellationToken ct = default)
    {
        var section = await sectionRepo.GetByIdAsync(sectionId, ct);
        if (section is null) return null;

        string? chapterTitle = null;
        if (section.ParentId.HasValue)
        {
            var parent = await sectionRepo.GetByIdAsync(section.ParentId.Value, ct);
            chapterTitle = parent?.Title;
        }

        var comments  = await commentService.GetThreadsForSectionAsync(sectionId, authorId, ct);
        var events    = await readEventRepository.GetBySectionIdAsync(sectionId, ct);
        var snapshots = await snapshotRepository.GetBySectionIdAsync(sectionId, ct);

        var nameMap = new Dictionary<Guid, string>();
        foreach (var uid in comments.Select(c => c.AuthorId).Distinct())
        {
            var u = await userRepository.GetByIdAsync(uid, ct);
            nameMap[uid] = u?.DisplayName ?? "Unknown";
        }

        var snapshotByUser = snapshots.ToDictionary(s => s.UserId);
        var readCurrentNames    = new List<string>();
        var notReadCurrentNames = new List<string>();

        foreach (var uid in events.Select(e => e.UserId).Distinct())
        {
            var u    = await userRepository.GetByIdAsync(uid, ct);
            var name = u?.DisplayName ?? "Unknown";
            if (snapshotByUser.TryGetValue(uid, out var snap) &&
                snap.HtmlContent == section.HtmlContent)
                readCurrentNames.Add(name);
            else
                notReadCurrentNames.Add(name);
        }

        return new SectionDetailDto
        {
            Section               = section,
            ChapterTitle          = chapterTitle,
            Comments              = comments,
            CommentAuthorNames    = nameMap,
            ReadCurrentCount      = readCurrentNames.Count,
            NotReadCurrentCount   = notReadCurrentNames.Count,
            ReadCurrentNames      = readCurrentNames,
            NotReadCurrentNames   = notReadCurrentNames
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
