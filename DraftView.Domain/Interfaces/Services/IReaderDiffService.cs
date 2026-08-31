using DraftView.Domain.Contracts;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Orchestrates reader-facing diff operations: retrieving the diff for display,
/// marking a section as read, and marking it as unread.
/// </summary>
public interface IReaderDiffService
{
    /// <summary>
    /// Returns the diff for a section for the given reader, applying their
    /// ShowDiffOnRevisit preference, cooldown, and ReadingStyle threshold.
    /// Returns null when: preferences not found, ShowDiffOnRevisit is false,
    /// cooldown is active, or the classification is below the reader's threshold.
    /// </summary>
    Task<SectionDiffResult?> GetDiffAsync(Guid sectionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Marks the section as read at the current latest version for the given reader.
    /// No-op if no ReadEvent or no published version exists.
    /// </summary>
    Task MarkAsReadAsync(Guid sectionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Reverses the last MarkAsRead for the section, restoring the previous diff baseline.
    /// No-op if no ReadEvent exists.
    /// </summary>
    Task MarkAsUnreadAsync(Guid sectionId, Guid userId, CancellationToken ct = default);
}
