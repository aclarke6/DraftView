namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Creates and manages SectionVersion snapshots when the author publishes or revokes content.
/// This is the only permitted path for SectionVersion creation.
/// Sync and import never call this service.
/// </summary>
public interface IVersioningService
{
    /// <summary>
    /// Creates a SectionVersion for each non-soft-deleted Document descendant
    /// of the given chapter. Sets Section.IsPublished = true on each versioned section.
    /// Throws if the chapter does not exist, is not a Folder, or has no publishable
    /// Document descendants.
    /// </summary>
    Task RepublishChapterAsync(
        Guid chapterId,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a SectionVersion for a single Document section.
    /// Sets Section.IsPublished = true.
    /// Throws if the section does not exist, is not a Document, is soft-deleted,
    /// or has no HtmlContent.
    /// </summary>
    Task RepublishSectionAsync(
        Guid sectionId,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes the latest SectionVersion for a single Document section.
    /// Rolls back to the previous version; that version becomes reader-visible.
    /// If no previous version exists, sets Section.IsPublished = false.
    /// Throws if the section does not exist, is not a Document, or has no versions.
    /// Revoke is not permitted when only one version exists and it is the
    /// current published version — use Unpublish instead.
    /// </summary>
    Task RevokeLatestVersionAsync(
        Guid sectionId,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Locks a chapter (Folder section), blocking all publish actions.
    /// Throws if the section does not exist or is not a Folder.
    /// </summary>
    Task LockChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

    /// <summary>
    /// Unlocks a chapter, re-enabling publish actions.
    /// Throws if the section does not exist, is not a Folder, or is not locked.
    /// </summary>
    Task UnlockChapterAsync(Guid chapterId, Guid authorId, CancellationToken ct = default);

    /// <summary>
    /// Sets a suggested publish date for a chapter.
    /// Scheduling is advisory — it never blocks the republish action.
    /// Throws if the section does not exist or is not a Folder.
    /// </summary>
    Task ScheduleChapterAsync(Guid chapterId, Guid authorId, DateTime scheduledAt,
        CancellationToken ct = default);

    /// <summary>
    /// Clears the scheduled publish date for a chapter.
    /// Throws if the section does not exist or is not a Folder.
    /// </summary>
    Task ClearScheduleAsync(Guid chapterId, Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a SectionVersion for a published Document whose content changed during a
    /// Scrivener sync. Called only by ScrivenerSyncService for ScrivenerDropbox projects.
    /// Silent no-op when the section is not published, not a Document, is soft-deleted,
    /// or the parent chapter is locked. Does not call SaveChanges — the sync service
    /// owns persistence for the entire sync cycle.
    /// </summary>
    Task CreateVersionFromSyncAsync(
        Domain.Entities.Section document,
        Guid authorId,
        CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes a single historical version.
    /// Throws <see cref="DraftView.Domain.Exceptions.InvariantViolationException"/> if
    /// <paramref name="versionId"/> is the current (latest) version for <paramref name="sectionId"/> —
    /// use <see cref="RevokeLatestVersionAsync"/> instead.
    /// </summary>
    Task DeleteVersionAsync(Guid versionId, Guid sectionId, CancellationToken ct = default);
}
