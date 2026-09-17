using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Domain.Interfaces.Services;
using DraftView.Domain.Notifications;

namespace DraftView.Application.Services;

public class DashboardService(
    ISectionRepository sectionRepo,
    IUserRepository userRepo,
    IEmailDeliveryLogRepository logRepo,
    ICommentRepository commentRepo,
    IReadEventRepository readEventRepo,
    IReaderAccessRepository readerAccessRepo,
    IAuthorNotificationRepository notificationRepo,
    IUnitOfWork unitOfWork) : IDashboardService
{
    private const string UngroupedChaptersTitle = "Ungrouped chapters";

    private static readonly IReadOnlyDictionary<NotificationFilterGroup, NotificationEventType[]> GroupTypes =
        new Dictionary<NotificationFilterGroup, NotificationEventType[]>
        {
            [NotificationFilterGroup.Comments] = [NotificationEventType.NewComment],
            [NotificationFilterGroup.Replies]  = [NotificationEventType.ReplyToAuthor],
            [NotificationFilterGroup.Readers]  = [
                NotificationEventType.ReaderJoined,
                NotificationEventType.ReaderReadNewScene,
                NotificationEventType.ReaderReturned,
                NotificationEventType.ReaderFinishedManuscript,
                NotificationEventType.AccessRequest,
            ],
            [NotificationFilterGroup.Sync] = [
                NotificationEventType.SyncCompleted,
                NotificationEventType.ChapterUploaded,
            ],
        };

    public async Task<IReadOnlyList<Section>> GetProjectOverviewAsync(
        Guid projectId, CancellationToken ct = default) =>
        await sectionRepo.GetPublishedByProjectIdAsync(projectId, ct);

    public async Task<IReadOnlyList<User>> GetReaderSummaryAsync(
        CancellationToken ct = default) =>
        await userRepo.GetAllBetaReadersAsync(ct);

    public async Task<IReadOnlyList<EmailDeliveryLog>> GetEmailHealthSummaryAsync(
        CancellationToken ct = default) =>
        await logRepo.GetFailedAsync(ct);

    /// <summary>
    /// Builds the hierarchical published-chapter progress data for the author dashboard.
    /// </summary>
    public async Task<AuthorDashboardProgressDto> GetPublishedChapterProgressAsync(
        Guid projectId, Guid authorId, CancellationToken ct = default)
    {
        var sections = await sectionRepo.GetByProjectIdAsync(projectId, ct);
        var sortedSections = SortDepthFirst(sections);
        var publishedChapters = GetPublishedLeafChapters(sortedSections);
        if (publishedChapters.Count == 0)
            return new AuthorDashboardProgressDto
            {
                UsesStructuralGroups = false,
                Groups = [],
                Chapters = []
            };

        var sectionById = sections.ToDictionary(section => section.Id);
        var commentsBySectionId = await LoadCommentsBySectionIdAsync(
            GetChapterScopeSectionIds(publishedChapters, sortedSections, sectionById),
            ct);
        var readEvents = await readEventRepo.GetByProjectIdAsync(projectId, ct);
        var readers = await LoadActiveReadersAsync(projectId, ct);
        var chapters = publishedChapters
            .Select(chapter => BuildChapterProgress(
                chapter,
                sortedSections,
                sectionById,
                commentsBySectionId,
                readEvents,
                readers,
                authorId))
            .ToList();

        if (!chapters.Any(chapter => HasStructuralParent(chapter.Chapter, sectionById)))
            return new AuthorDashboardProgressDto
            {
                UsesStructuralGroups = false,
                Groups = [],
                Chapters = chapters
            };

        return new AuthorDashboardProgressDto
        {
            UsesStructuralGroups = true,
            Groups = BuildGroups(chapters, sectionById),
            Chapters = []
        };
    }

    /// <summary>
    /// Returns notifications for the author, pruning any older than 90 days first.
    /// When a filter group is provided, only notifications belonging to that group are returned.
    /// </summary>
    public async Task<IReadOnlyList<AuthorNotification>> GetNotificationsAsync(
        Guid authorId, NotificationFilterGroup? group = null, CancellationToken ct = default)
    {
        await notificationRepo.PruneOlderThanAsync(authorId, DateTime.UtcNow.AddDays(-90), ct);
        return group.HasValue
            ? await notificationRepo.GetByAuthorIdAndTypesAsync(authorId, GroupTypes[group.Value], ct)
            : (await notificationRepo.GetByAuthorIdAsync(authorId, ct))
                .Where(notification => notification.EventType != NotificationEventType.SyncCompleted)
                .ToList();
    }

    public async Task DismissNotificationAsync(
        Guid notificationId, CancellationToken ct = default)
    {
        await notificationRepo.DeleteAsync(notificationId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DismissAllNotificationsAsync(
        Guid authorId, CancellationToken ct = default)
    {
        await notificationRepo.DeleteAllByAuthorIdAsync(authorId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes notifications for the author scoped by type.
    /// When type is null, all notifications for the author are deleted.
    /// </summary>
    public async Task DismissNotificationsByTypeAsync(
        Guid authorId, NotificationEventType? type, CancellationToken ct = default)
    {
        if (type.HasValue)
            await notificationRepo.DeleteByAuthorIdAndTypeAsync(authorId, type.Value, ct);
        else
            await notificationRepo.DeleteAllByAuthorIdAsync(authorId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Loads all active readers for the project and resolves their display names.
    /// </summary>
    private async Task<IReadOnlyList<(Guid ReaderId, string ReaderName)>> LoadActiveReadersAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var accessRecords = await readerAccessRepo.GetByProjectIdAsync(projectId, ct);
        var readers = new List<(Guid ReaderId, string ReaderName)>();

        foreach (var access in accessRecords
                     .Where(access => access.IsActive)
                     .GroupBy(access => access.ReaderId)
                     .Select(group => group.First()))
        {
            var reader = await userRepo.GetByIdAsync(access.ReaderId, ct);
            if (reader is null || reader.IsSoftDeleted)
                continue;

            readers.Add((reader.Id, reader.DisplayName));
        }

        return readers
            .OrderBy(reader => reader.ReaderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Loads all comments for every chapter-scoped section needed by the dashboard.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Comment>>> LoadCommentsBySectionIdAsync(
        IReadOnlyCollection<Guid> sectionIds,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, IReadOnlyList<Comment>>();

        foreach (var sectionId in sectionIds)
        {
            var comments = await commentRepo.GetAllBySectionIdAsync(sectionId, ct);
            result[sectionId] = comments
                .Where(comment => !comment.IsSoftDeleted)
                .ToList();
        }

        return result;
    }

    /// <summary>
    /// Returns the distinct chapter and embedded-scene section ids needed for comment aggregation.
    /// </summary>
    private static IReadOnlyCollection<Guid> GetChapterScopeSectionIds(
        IReadOnlyList<Section> chapters,
        IReadOnlyList<Section> sortedSections,
        IReadOnlyDictionary<Guid, Section> sectionById)
    {
        var sectionIds = new HashSet<Guid>();
        foreach (var chapter in chapters)
        {
            foreach (var sectionId in GetChapterScopeSections(chapter, sortedSections, sectionById)
                         .Select(section => section.Id))
                sectionIds.Add(sectionId);
        }

        return sectionIds;
    }

    /// <summary>
    /// Builds a single published chapter row, including reader rows and latest comment metadata.
    /// </summary>
    private static AuthorDashboardChapterProgressDto BuildChapterProgress(
        Section chapter,
        IReadOnlyList<Section> sortedSections,
        IReadOnlyDictionary<Guid, Section> sectionById,
        IReadOnlyDictionary<Guid, IReadOnlyList<Comment>> commentsBySectionId,
        IReadOnlyList<ReadEvent> readEvents,
        IReadOnlyList<(Guid ReaderId, string ReaderName)> readers,
        Guid authorId)
    {
        var scopeSections = GetChapterScopeSections(chapter, sortedSections, sectionById);
        var scopeSectionIds = scopeSections.Select(section => section.Id).ToHashSet();
        var chapterComments = scopeSectionIds
            .SelectMany(sectionId => commentsBySectionId.GetValueOrDefault(sectionId, []))
            .OrderBy(comment => comment.CreatedAt)
            .ToList();
        var chapterReadEvents = readEvents
            .Where(readEvent => scopeSectionIds.Contains(readEvent.SectionId))
            .ToList();
        var readerRows = readers
            .Select(reader => BuildReaderProgress(reader, chapterComments, chapterReadEvents))
            .ToList();
        var latestComment = chapterComments
            .OrderByDescending(comment => comment.CreatedAt)
            .FirstOrDefault();

        return new AuthorDashboardChapterProgressDto
        {
            Chapter = chapter,
            ViewedReaderCount = readerRows.Count(reader => reader.HasViewed),
            TotalReaderCount = readerRows.Count,
            LatestViewAt = chapterReadEvents.Count == 0
                ? null
                : chapterReadEvents.Max(readEvent => readEvent.LastOpenedAt),
            CommentCount = chapterComments.Count,
            NewCommentCount = chapterComments.Count(comment =>
                comment.AuthorId != authorId &&
                comment.Status == CommentStatus.New),
            LatestComment = latestComment is null
                ? null
                : new AuthorDashboardCommentLinkDto
                {
                    SectionId = latestComment.SectionId,
                    CommentId = latestComment.Id,
                    SectionTitle = sectionById.TryGetValue(latestComment.SectionId, out var commentSection)
                        ? commentSection.Title
                        : chapter.Title,
                    CreatedAt = latestComment.CreatedAt
                },
            Readers = readerRows
        };
    }

    /// <summary>
    /// Builds a single reader row for a chapter based on viewing and comment activity.
    /// </summary>
    private static AuthorDashboardReaderProgressDto BuildReaderProgress(
        (Guid ReaderId, string ReaderName) reader,
        IReadOnlyList<Comment> chapterComments,
        IReadOnlyList<ReadEvent> chapterReadEvents)
    {
        var lastViewedAt = chapterReadEvents
            .Where(readEvent => readEvent.UserId == reader.ReaderId)
            .Select(readEvent => (DateTime?)readEvent.LastOpenedAt)
            .Max();

        return new AuthorDashboardReaderProgressDto
        {
            ReaderId = reader.ReaderId,
            ReaderName = reader.ReaderName,
            HasViewed = lastViewedAt.HasValue,
            LastViewedAt = lastViewedAt,
            CommentCount = chapterComments.Count(comment => comment.AuthorId == reader.ReaderId)
        };
    }

    /// <summary>
    /// Builds the top-level group rows when the manuscript has a parent folder level above chapters.
    /// </summary>
    private static IReadOnlyList<AuthorDashboardGroupDto> BuildGroups(
        IReadOnlyList<AuthorDashboardChapterProgressDto> chapters,
        IReadOnlyDictionary<Guid, Section> sectionById)
    {
        var groupedChapters = new List<(string Title, bool IsUngrouped, List<AuthorDashboardChapterProgressDto> Chapters)>();

        foreach (var chapter in chapters)
        {
            var title = TryGetStructuralParentTitle(chapter.Chapter, sectionById) ?? UngroupedChaptersTitle;
            var isUngrouped = title == UngroupedChaptersTitle;
            var existingGroup = groupedChapters.FirstOrDefault(group =>
                group.Title == title &&
                group.IsUngrouped == isUngrouped);

            if (existingGroup.Chapters is null)
            {
                existingGroup = (title, isUngrouped, []);
                groupedChapters.Add(existingGroup);
            }

            existingGroup.Chapters.Add(chapter);
        }

        return groupedChapters
            .Select(group => new AuthorDashboardGroupDto
            {
                Title = group.Title,
                IsUngrouped = group.IsUngrouped,
                ViewedReaderChapterCount = group.Chapters.Sum(chapter => chapter.ViewedReaderCount),
                TotalReaderChapterCount = group.Chapters.Sum(chapter => chapter.TotalReaderCount),
                CommentCount = group.Chapters.Sum(chapter => chapter.CommentCount),
                NewCommentCount = group.Chapters.Sum(chapter => chapter.NewCommentCount),
                LatestActivityAt = group.Chapters.Select(GetLatestActivityAt).Max(),
                Chapters = group.Chapters
            })
            .ToList();
    }

    /// <summary>
    /// Returns the latest relevant timestamp for a chapter from viewing or comment activity.
    /// </summary>
    private static DateTime? GetLatestActivityAt(AuthorDashboardChapterProgressDto chapter) =>
        new[] { chapter.LatestViewAt, chapter.LatestComment?.CreatedAt }
            .Where(timestamp => timestamp.HasValue)
            .Max();

    /// <summary>
    /// Returns the chapter itself plus its embedded published scenes.
    /// </summary>
    private static IReadOnlyList<Section> GetChapterScopeSections(
        Section chapter,
        IReadOnlyList<Section> sortedSections,
        IReadOnlyDictionary<Guid, Section> sectionById)
    {
        var scope = new List<Section> { chapter };
        scope.AddRange(sortedSections.Where(section =>
            section.NodeType == NodeType.Document &&
            section.IsPublished &&
            !section.IsSoftDeleted &&
            IsDescendantOf(section, chapter.Id, sectionById)));

        return scope;
    }

    /// <summary>
    /// Returns true when the candidate section descends from the target chapter.
    /// </summary>
    private static bool IsDescendantOf(
        Section candidate,
        Guid chapterId,
        IReadOnlyDictionary<Guid, Section> sectionById)
    {
        var parentId = candidate.ParentId;
        while (parentId.HasValue)
        {
            if (parentId.Value == chapterId)
                return true;

            if (!sectionById.TryGetValue(parentId.Value, out var parent))
                return false;

            parentId = parent.ParentId;
        }

        return false;
    }

    /// <summary>
    /// Returns the display title for the structural parent above a chapter, if one exists.
    /// </summary>
    private static string? TryGetStructuralParentTitle(
        Section chapter,
        IReadOnlyDictionary<Guid, Section> sectionById)
    {
        if (!chapter.ParentId.HasValue)
            return null;

        return sectionById.TryGetValue(chapter.ParentId.Value, out var parent) &&
               parent.NodeType == NodeType.Folder &&
               !parent.IsSoftDeleted
            ? parent.Title
            : null;
    }

    /// <summary>
    /// Returns true when the chapter belongs beneath a structural parent row.
    /// </summary>
    private static bool HasStructuralParent(
        Section chapter,
        IReadOnlyDictionary<Guid, Section> sectionById) =>
        TryGetStructuralParentTitle(chapter, sectionById) is not null;

    /// <summary>
    /// Returns published leaf chapters in depth-first manuscript order.
    /// </summary>
    private static IReadOnlyList<Section> GetPublishedLeafChapters(IReadOnlyList<Section> sortedSections)
    {
        var folderChildIds = sortedSections
            .Where(section => section.NodeType == NodeType.Folder && section.ParentId.HasValue)
            .Select(section => section.ParentId!.Value)
            .ToHashSet();

        return sortedSections
            .Where(section =>
                section.NodeType == NodeType.Folder &&
                section.IsPublished &&
                !section.IsSoftDeleted &&
                !folderChildIds.Contains(section.Id))
            .ToList();
    }

    /// <summary>
    /// Returns all sections in depth-first order using SortOrder within each sibling group.
    /// </summary>
    private static IReadOnlyList<Section> SortDepthFirst(IReadOnlyList<Section> sections)
    {
        var root = Guid.Empty;
        var lookup = new Dictionary<Guid, List<Section>>();

        foreach (var section in sections)
        {
            var key = section.ParentId ?? root;
            if (!lookup.ContainsKey(key))
                lookup[key] = [];

            lookup[key].Add(section);
        }

        foreach (var key in lookup.Keys.ToList())
            lookup[key] = [.. lookup[key].OrderBy(section => section.SortOrder)];

        var ordered = new List<Section>();

        void Walk(Guid parentId)
        {
            if (!lookup.TryGetValue(parentId, out var children))
                return;

            foreach (var child in children)
            {
                ordered.Add(child);
                Walk(child.Id);
            }
        }

        Walk(root);
        return ordered;
    }
}
