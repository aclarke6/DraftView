namespace DraftView.Domain.Enumerations;

/// <summary>
/// Classifies the nature of changes between two SectionVersion snapshots.
/// Ordinal order is significant: Trivial &lt; Polish &lt; Revision &lt; Rewrite.
/// Populated by IChangeClassificationService.
/// </summary>
public enum ChangeClassification
{
    Trivial  = -1,  // < max(5, totalWords * 0.01) words changed — micro-fixes only
    Polish   =  0,  // 1–10% of words changed — light surface improvement
    Revision =  1,  // 10–40% of words changed — noticeable reworking
    Rewrite  =  2   // > 40% of words changed — major restructuring
}
