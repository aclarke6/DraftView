using DraftView.Domain.Contracts;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Evaluates whether the current caller may reject or relink a passage anchor.
/// </summary>
public interface IHumanOverrideService
{
    Task EnsureCanRejectAsync(Guid anchorId, Guid currentUserId, CancellationToken ct = default);

    Task EnsureCanRelinkAsync(Guid anchorId, Guid currentUserId, CancellationToken ct = default);

    Task<PassageAnchorDto> RejectAsync(
        Guid anchorId,
        Guid currentUserId,
        string? reason = null,
        CancellationToken ct = default);

    Task<PassageAnchorDto> RelinkAsync(
        Guid anchorId,
        CreatePassageAnchorRequest relinkRequest,
        Guid currentUserId,
        CancellationToken ct = default);
}
