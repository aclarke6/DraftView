using DraftView.Domain.Contracts;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Creates and retrieves passage anchors through the application boundary.
/// </summary>
public interface IPassageAnchorService
{
    Task ValidateSelectionAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default);

    Task<PassageAnchorDto> CreateAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the current exact relocation target for an anchor against the latest
    /// reader-visible version of its section. Returns null when exact matching is
    /// ambiguous or unavailable.
    /// </summary>
    Task<PassageAnchorMatchDto?> TryResolveExactMatchAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves an anchor using deterministic prefix/suffix context when exact matching
    /// is ambiguous. Returns null when context is insufficient to choose safely.
    /// </summary>
    Task<PassageAnchorMatchDto?> TryResolveContextMatchAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default);

    Task<PassageAnchorDto> GetByIdAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default);
}
