namespace DraftView.Domain.Contracts;

/// <summary>
/// Audit metadata for a human rejection of a passage anchor location.
/// </summary>
public sealed record PassageAnchorRejectionDto(
    Guid RejectedByUserId,
    DateTime RejectedAt,
    string? Reason);
