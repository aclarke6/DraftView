namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Result of adding a batch of discovered Dropbox/Scrivener projects for an
/// author: how many were added (new or restored from soft-delete) and, when
/// exactly one was added, its display name for a friendly confirmation message.
/// </summary>
public sealed class AddDiscoveredProjectsResultDto
{
    public required int AddedCount { get; init; }
    public string? SingleAddedProjectName { get; init; }
}

public interface IProjectManagementService
{
    /// <summary>
    /// Adds the discovered projects identified by <paramref name="selectedUuids"/>
    /// for the given author: restores any matching soft-deleted project, or
    /// creates a new one. Newly added projects are queued for background
    /// synchronization. Projects already added, or not found among the
    /// author's discovered projects, are ignored.
    /// </summary>
    Task<AddDiscoveredProjectsResultDto> AddDiscoveredProjectsAsync(
        IReadOnlyList<string> selectedUuids, Guid authorId, CancellationToken ct = default);
}
