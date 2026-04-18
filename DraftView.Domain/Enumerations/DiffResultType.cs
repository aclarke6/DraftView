namespace DraftView.Domain.Enumerations;

/// <summary>
/// Classifies a paragraph in a diff result.
/// </summary>
public enum DiffResultType
{
    Unchanged = 0,
    Added     = 1,
    Removed   = 2
}
