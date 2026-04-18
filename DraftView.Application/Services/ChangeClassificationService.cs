using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

/// <summary>
/// Classifies the nature of content changes from paragraph-level diff results.
/// </summary>
public class ChangeClassificationService : IChangeClassificationService
{
    private const double RewriteThreshold = 0.6;
    private const double RevisionThreshold = 0.2;

    /// <summary>
    /// Classifies changes based on a paragraph-level diff result.
    /// Returns null when no diff exists (no previous version).
    /// </summary>
    public ChangeClassification? Classify(IReadOnlyList<ParagraphDiffResult> diffParagraphs)
    {
        if (diffParagraphs is null || diffParagraphs.Count == 0)
            return null;

        var total = diffParagraphs.Count;
        var added = diffParagraphs.Count(p => p.Type == DiffResultType.Added);
        var removed = diffParagraphs.Count(p => p.Type == DiffResultType.Removed);
        var changed = added + removed;

        var changedRatio = (double)changed / total;

        if (changedRatio >= RewriteThreshold)
            return ChangeClassification.Rewrite;

        if (changedRatio >= RevisionThreshold)
            return ChangeClassification.Revision;

        if (changedRatio > 0)
            return ChangeClassification.Polish;

        return null;
    }
}
