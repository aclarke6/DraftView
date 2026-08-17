using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ReaderDashboardService(
    IReadingProgressService progressService,
    ISectionRepository sectionRepo,
    ICommentRepository commentRepo) : IReaderDashboardService
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
}
