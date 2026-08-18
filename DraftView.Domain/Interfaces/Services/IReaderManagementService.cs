using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>Aggregated row for a single beta reader on the Readers management page.</summary>
public sealed record ReaderSummaryRow(
    Guid Id,
    string DisplayName,
    ReaderStatus Status,
    DateTime? ActivatedAt,
    bool HasPendingInvitation);

/// <summary>
/// Provides a summary of all beta readers for the author's Readers management page.
/// </summary>
public interface IReaderManagementService
{
    /// <summary>
    /// Returns all non-soft-deleted beta readers with their derived status and pending
    /// invitation state, ordered by display name.
    /// </summary>
    Task<IReadOnlyList<ReaderSummaryRow>> GetReaderSummaryAsync(CancellationToken ct = default);
}
