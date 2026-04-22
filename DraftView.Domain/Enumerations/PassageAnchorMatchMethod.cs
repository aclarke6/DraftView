namespace DraftView.Domain.Enumerations;

/// <summary>
/// Identifies how a current passage anchor match was produced.
/// </summary>
public enum PassageAnchorMatchMethod
{
    Exact = 0,
    Context = 1,
    Fuzzy = 2,
    Ai = 3,
    ManualRelink = 4,
    Rejected = 5,
    Orphaned = 6
}
