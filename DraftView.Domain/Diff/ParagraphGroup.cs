using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Diff;

/// <summary>
/// A merged group of paragraphs for threshold-filtered diff rendering.
/// Short paragraphs are merged until the group meets the minimum word count.
/// Each group is classified independently; ShowDiff indicates whether it
/// meets the reader's ReadingStyle threshold (or the fallback rule).
/// </summary>
public sealed record ParagraphGroup(
    IReadOnlyList<ParagraphDiffResult> Paragraphs,
    ChangeClassification? Classification,
    bool ShowDiff);
