namespace DraftView.Domain.Enumerations;

/// <summary>
/// Classifies the nature of changes between two SectionVersion snapshots.
/// Populated by IChangeClassificationService in V-Sprint 4.
/// </summary>
public enum ChangeClassification
{
    Polish = 0,
    Revision = 1,
    Rewrite = 2
}
