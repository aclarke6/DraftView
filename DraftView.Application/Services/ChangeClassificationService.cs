using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Classifies the nature of content changes from word-level diff results.
/// Uses total word count and changed word count to assign Trivial, Polish, Revision, or Rewrite.
/// </summary>
public class ChangeClassificationService : IChangeClassificationService
{
    private const double PolishRevisionBoundary = 0.10;
    private const double RevisionRewriteBoundary = 0.40;
    private const double TrivialPercentageThreshold = 0.01;
    private const int TrivialAbsoluteFloor = 5;

    /// <summary>
    /// Classifies changes based on word-level diff results.
    /// Returns null when no changes exist (wordsChanged == 0 or no paragraphs).
    /// Trivial threshold: wordsChanged less than max(5, ceil(totalWords * 1%)).
    /// Polish: 1-10%. Revision: 10-40%. Rewrite: 40%+.
    /// </summary>
    public ChangeClassification? Classify(IReadOnlyList<ParagraphDiffResult> diffParagraphs)
    {
        if (diffParagraphs is null || diffParagraphs.Count == 0)
            return null;

        int totalWords   = diffParagraphs.Sum(p => p.TotalWords);
        int wordsChanged = diffParagraphs.Sum(p => p.WordsAdded + p.WordsRemoved);

        if (wordsChanged == 0 || totalWords == 0)
            return null;

        int trivialThreshold = TrivialThreshold(totalWords);

        if (wordsChanged < trivialThreshold)
            return ChangeClassification.Trivial;

        double ratio = (double)wordsChanged / totalWords;

        if (ratio < PolishRevisionBoundary)
            return ChangeClassification.Polish;

        if (ratio < RevisionRewriteBoundary)
            return ChangeClassification.Revision;

        return ChangeClassification.Rewrite;
    }

    private static int TrivialThreshold(int totalWords)
        => Math.Max(TrivialAbsoluteFloor, (int)Math.Ceiling(totalWords * TrivialPercentageThreshold));
}
