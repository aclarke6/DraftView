namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Creates SectionVersion snapshots when the author publishes a chapter.
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
}
