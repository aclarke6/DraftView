using DraftView.Domain.Contracts;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Retrieves original context for passage anchors.
/// </summary>
public interface IOriginalContextService
{
    Task<OriginalContextResultDto> GetOriginalContextAsync(
        Guid passageAnchorId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);
}
