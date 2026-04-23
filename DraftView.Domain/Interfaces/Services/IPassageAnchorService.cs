using DraftView.Domain.Contracts;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Creates and retrieves passage anchors through the application boundary.
/// </summary>
public interface IPassageAnchorService
{
    Task<PassageAnchorDto> CreateAsync(
        CreatePassageAnchorRequest request,
        Guid currentUserId,
        CancellationToken ct = default);

    Task<PassageAnchorDto> GetByIdAsync(
        Guid anchorId,
        Guid currentUserId,
        CancellationToken ct = default);
}
