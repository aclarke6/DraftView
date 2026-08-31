namespace DraftView.Domain.Enumerations;

/// <summary>
/// Classifies the change state between what a reader last read and current content.
/// Ordinal order is significant: Trivial &lt; Polish &lt; Revision &lt; Rewrite &lt; New.
/// New (never read) is the highest-priority state — the reader has no baseline.
/// Computed at page view time; never stored.
/// </summary>
public enum ChangeClassification
{
    Trivial  = 0,  // < max(5, totalWords * 0.01) words changed — micro-fixes only
    Polish   = 1,  // 1–10% of words changed — light surface improvement
    Revision = 2,  // 10–40% of words changed — noticeable reworking
    Rewrite  = 3,  // > 40% of words changed — major restructuring
    New      = 4   // reader has never read this scene — no baseline exists
}
