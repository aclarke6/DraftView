namespace DraftView.Domain.Enumerations;

/// <summary>
/// Represents the current trust state of a passage anchor.
/// </summary>
public enum PassageAnchorStatus
{
    Original = 0,
    Exact = 1,
    Context = 2,
    Fuzzy = 3,
    AiMatched = 4,
    UserRelinked = 5,
    Rejected = 6,
    Orphaned = 7
}
