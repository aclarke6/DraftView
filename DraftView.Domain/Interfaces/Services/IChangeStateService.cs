using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Computes the reader-centric change state for a scene at page-view time.
/// Never stored — always computed from the reader's snapshot vs current content.
/// </summary>
public interface IChangeStateService
{
    /// <summary>
    /// Returns the change state for a reader's view of a scene:
    /// - New      : reader has no snapshot (never read or not yet marked read)
    /// - null     : snapshot matches current content — reader is fully up to date
    /// - Trivial/Polish/Revision/Rewrite : content has changed since the reader last read it
    /// </summary>
    Task<ChangeClassification?> GetChangeStateAsync(
        Guid sectionId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns both the change classification and the paragraph-level diff results.
    /// Used by the read view to populate diff toggle and margin indicators.
    /// Paragraphs is empty when classification is null (up to date) or New (no baseline).
    /// </summary>
    Task<(ChangeClassification? Classification, IReadOnlyList<ParagraphDiffResult> Paragraphs)>
        GetChangeStateWithDiffAsync(
            Guid sectionId,
            Guid userId,
            CancellationToken ct = default);
}
