using DraftView.Domain.Entities;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates manual chapter upload, edit, reorder, replace, and version history management.
/// </summary>
public interface IManualUploadService
{
    /// <summary>
    /// Parses and stores a chapter from a file upload. Rejects if the project is not Manual.
    /// Enforces the 250 chapter-per-project limit. Emits an AuthorNotification on success.
    /// </summary>
    Task<ManualChapter> UploadChapterAsync(
        Guid projectId, Guid authorId, string title,
        Stream content, string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Stores a chapter contributed via cut/paste. OriginalFileName is null.
    /// Emits an AuthorNotification on success.
    /// </summary>
    Task<ManualChapter> UploadChapterFromPasteAsync(
        Guid projectId, Guid authorId, string title,
        string rawContent,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns new SortOrder values to all chapters in the project according to the supplied order.
    /// </summary>
    Task ReorderChaptersAsync(
        Guid projectId, Guid authorId,
        IReadOnlyList<Guid> orderedChapterIds,
        CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes the specified chapter.
    /// </summary>
    Task DeleteChapterAsync(
        Guid chapterId, Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Replaces a chapter's content from a new file, creating a version snapshot first.
    /// </summary>
    Task<ManualChapter> ReplaceChapterAsync(
        Guid chapterId, Guid authorId,
        Stream content, string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a chapter's content via inline edit, creating a version snapshot with
    /// reason InlineEdit before overwriting.
    /// </summary>
    Task<ManualChapter> EditChapterAsync(
        Guid chapterId, Guid authorId,
        string rawContent,
        CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes all ManualChapterVersion records for the chapter.
    /// The live chapter content is not affected.
    /// </summary>
    Task ClearVersionHistoryAsync(
        Guid chapterId, Guid authorId,
        CancellationToken ct = default);
}
