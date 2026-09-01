using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Domain.Interfaces.Services;

/// <summary>
/// Merges paragraph diff results into groups of at least MinDiffGroupWords words,
/// classifies each group, and applies the reader's ReadingStyle threshold.
/// Groups below the threshold have ShowDiff = false (rendered as clean prose).
/// Fallback rule: if no group meets the threshold, groups at the highest available
/// classification are shown so the reader always has at least something to anchor on.
/// </summary>
public interface IParagraphGroupingService
{
    IReadOnlyList<ParagraphGroup> Group(
        IReadOnlyList<ParagraphDiffResult> paragraphs,
        ReadingStyle readingStyle,
        int minGroupWords);
}
