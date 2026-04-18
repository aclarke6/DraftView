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
}
