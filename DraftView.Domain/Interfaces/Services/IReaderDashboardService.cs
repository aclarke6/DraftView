using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

public record ResumeTarget(Guid ChapterId, Guid? SceneId);

public interface IReaderDashboardService
{
    /// <summary>
    /// Returns the most-recently-opened section across the given projects,
    /// resolved to a (chapter, optional scene) target. Returns null when
    /// the user has no read history across these projects, or the last-read
    /// section is no longer published.
    /// </summary>
    Task<ResumeTarget?> GetCrossProjectResumeTargetAsync(
        Guid userId, IReadOnlyList<Guid> projectIds, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of root comments (not replies, not soft-deleted)
    /// the reader has placed on or within each chapter (chapter + its scenes).
    /// Every requested chapterId appears as a key; zero when no comments found.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetReaderChapterCommentCountsAsync(
        Guid userId, IReadOnlyList<Guid> chapterIds, CancellationToken ct = default);

    /// <summary>
    /// Returns true for a chapter if the reader has any ReadEvent on the chapter
    /// folder OR on any descendant Document scene. Covers both the desktop path
    /// (where the chapter folder gets a ReadEvent) and the mobile path (where only
    /// scene nodes get ReadEvents). Every requested chapterId appears as a key.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool>> GetChapterHasReadStatusesAsync(
        Guid userId, IReadOnlyList<Guid> chapterIds, CancellationToken ct = default);

    /// <summary>
    /// Returns the highest ChangeClassification across all scenes updated since
    /// the reader last read them, filtered by the reader's ReadingStyle threshold.
    /// Null means the reader is up to date (or has never read) at their threshold level.
    /// Every requested chapterId appears as a key.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ChangeClassification?>> GetChapterChangeStatusesAsync(
        Guid userId, IReadOnlyList<Guid> chapterIds, ReadingStyle readingStyle, CancellationToken ct = default);
}
