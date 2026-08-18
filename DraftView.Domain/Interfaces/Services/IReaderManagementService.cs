using DraftView.Domain.Entities;
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
/// All data required to render the ManageReaderAccess page: the reader's identity,
/// derived status, and the partition of projects into those with and without access.
/// </summary>
public sealed class ReaderAccessData
{
    public required Guid ReaderId { get; init; }
    public required string DisplayName { get; init; }
    public required ReaderStatus Status { get; init; }
    public required IReadOnlyList<Project> ProjectsWithAccess { get; init; }
    public required IReadOnlyList<Project> ProjectsWithoutAccess { get; init; }
}

/// <summary>
/// Provides reader management operations for the author's Readers management page.
/// </summary>
public interface IReaderManagementService
{
    /// <summary>
    /// Returns all non-soft-deleted beta readers with their derived status and pending
    /// invitation state, ordered by display name.
    /// </summary>
    Task<IReadOnlyList<ReaderSummaryRow>> GetReaderSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the reader access data for the ManageReaderAccess page, including
    /// the reader's derived status and the project partition. Returns null when
    /// the reader does not exist.
    /// </summary>
    Task<ReaderAccessData?> GetReaderAccessDataAsync(Guid readerId, CancellationToken ct = default);

    /// <summary>
    /// Grants access to projects in <paramref name="grantIds"/> and revokes access
    /// to projects in <paramref name="revokeIds"/> for the given reader.
    /// </summary>
    Task UpdateReaderAccessAsync(Guid readerId, IReadOnlyList<Guid> grantIds,
        IReadOnlyList<Guid> revokeIds, Guid authorId, CancellationToken ct = default);

    /// <summary>
    /// Revokes all project access for the reader, then soft-deletes the reader record.
    /// </summary>
    Task SoftDeleteReaderAsync(Guid userId, Guid authorId, CancellationToken ct = default);
}
