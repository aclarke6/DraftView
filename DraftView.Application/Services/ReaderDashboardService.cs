using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ReaderDashboardService(
    IReadingProgressService progressService,
    ISectionRepository sectionRepo,
    ICommentRepository commentRepo,
    IReadEventRepository readEventRepo,
    ISectionVersionRepository sectionVersionRepo) : IReaderDashboardService
{
    public async Task<ResumeTarget?> GetCrossProjectResumeTargetAsync(
        Guid userId, IReadOnlyList<Guid> projectIds, CancellationToken ct = default)
    {
        if (projectIds.Count == 0)
            return null;

        ReadEvent? latest = null;
        foreach (var projectId in projectIds)
        {
            var ev = await progressService.GetLastReadEventAsync(userId, projectId, ct);
            if (ev is not null && (latest is null || ev.LastOpenedAt > latest.LastOpenedAt))
                latest = ev;
        }

        if (latest is null)
            return null;

        var section = await sectionRepo.GetByIdAsync(latest.SectionId, ct);
        if (section is null || !section.IsPublished)
            return null;

        if (section.NodeType == NodeType.Document && section.ParentId.HasValue)
        {
            var parent = await sectionRepo.GetByIdAsync(section.ParentId.Value, ct);
            if (parent is null || !parent.IsPublished)
                return null;
            return new ResumeTarget(parent.Id, section.Id);
        }

        if (section.NodeType == NodeType.Folder)
            return new ResumeTarget(section.Id, null);

        return null;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetReaderChapterCommentCountsAsync(
        Guid userId, IReadOnlyList<Guid> chapterIds, CancellationToken ct = default)
    {
        if (chapterIds.Count == 0)
            return new Dictionary<Guid, int>();

        var allComments = await commentRepo.GetByAuthorIdAsync(userId, ct);
        var rootComments = allComments
            .Where(c => c.ParentCommentId == null && !c.IsSoftDeleted)
            .ToList();

        var result = new Dictionary<Guid, int>();

        foreach (var chapterId in chapterIds)
        {
            if (result.ContainsKey(chapterId))
                continue;

            var count = rootComments.Count(c => c.SectionId == chapterId);

            var descendants = await sectionRepo.GetAllDescendantsAsync(chapterId, ct);
            var descendantIds = descendants.Select(s => s.Id).ToHashSet();
            count += rootComments.Count(c => descendantIds.Contains(c.SectionId));

            result[chapterId] = count;
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> GetChapterHasReadStatusesAsync(
        Guid userId, IReadOnlyList<Guid> chapterIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, bool>();

        if (chapterIds.Count == 0)
            return result;

        foreach (var chapterId in chapterIds)
        {
            if (result.ContainsKey(chapterId))
                continue;

            if (await readEventRepo.HasReadAsync(chapterId, userId, ct))
            {
                result[chapterId] = true;
                continue;
            }

            var descendants = await sectionRepo.GetAllDescendantsAsync(chapterId, ct);
            var hasReadScene = false;
            foreach (var scene in descendants.Where(s => s.NodeType == NodeType.Document))
            {
                if (await readEventRepo.HasReadAsync(scene.Id, userId, ct))
                {
                    hasReadScene = true;
                    break;
                }
            }

            result[chapterId] = hasReadScene;
        }

        return result;
    }

    /// <summary>
    /// Returns the highest ChangeClassification across all Document scenes in each chapter
    /// that have been updated since the reader's last read version, filtered by ReadingStyle.
    /// Uses the latest SectionVersion.ChangeClassification as an approximation.
    /// Returns null for a chapter when the reader is up to date at their threshold level.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ChangeClassification?>> GetChapterChangeStatusesAsync(
        Guid userId, IReadOnlyList<Guid> chapterIds, ReadingStyle readingStyle, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, ChangeClassification?>();

        if (chapterIds.Count == 0)
            return result;

        var minimumTier = MinimumTier(readingStyle);

        foreach (var chapterId in chapterIds)
        {
            if (result.ContainsKey(chapterId))
                continue;

            var scenes = await sectionRepo.GetAllDescendantsAsync(chapterId, ct);
            var documents = scenes.Where(s => s.NodeType == NodeType.Document && s.IsPublished && !s.IsSoftDeleted);

            ChangeClassification? chapterMax = null;

            foreach (var scene in documents)
            {
                var readEvent = await readEventRepo.GetAsync(scene.Id, userId, ct);
                if (readEvent is null)
                    continue;

                var latestVersion = await sectionVersionRepo.GetLatestAsync(scene.Id, ct);
                if (latestVersion is null)
                    continue;

                // Null LastReadVersionNumber = baseline unknown (pre-versioning read or backfill).
                // Any published version counts as pending for this reader.
                if (readEvent.LastReadVersionNumber is not null &&
                    latestVersion.VersionNumber <= readEvent.LastReadVersionNumber)
                    continue;

                var classification = latestVersion.ChangeClassification;

                // When the reader has no confirmed read version, we cannot verify the
                // version content matches what they actually read. Default to Polish so
                // any unclassified version (e.g. version 1 with no baseline to diff
                // against) still surfaces as a badge rather than silently showing "Read".
                if (readEvent.LastReadVersionNumber is null)
                    classification ??= ChangeClassification.Polish;

                if (classification is null || classification < minimumTier)
                    continue;

                if (chapterMax is null || classification > chapterMax)
                    chapterMax = classification;
            }

            result[chapterId] = chapterMax;
        }

        return result;
    }

    private static ChangeClassification MinimumTier(ReadingStyle style) => style switch
    {
        ReadingStyle.BetaReader    => ChangeClassification.Trivial,
        ReadingStyle.StoryReader   => ChangeClassification.Polish,
        ReadingStyle.AlphaReader   => ChangeClassification.Revision,
        ReadingStyle.StructureOnly => ChangeClassification.Rewrite,
        _                          => ChangeClassification.Polish
    };
}
