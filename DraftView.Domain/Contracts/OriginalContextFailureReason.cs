namespace DraftView.Domain.Contracts;

/// <summary>
/// Reasons why original context retrieval may fail.
/// </summary>
public enum OriginalContextFailureReason
{
    NotFound,
    Unauthorized,
    OriginalContentMissing
}
