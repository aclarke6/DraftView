using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Application.Services;

public class ParagraphGroupingService : IParagraphGroupingService
{
    private readonly IChangeClassificationService _classifier;

    public ParagraphGroupingService() : this(new ChangeClassificationService()) { }

    public ParagraphGroupingService(IChangeClassificationService classifier)
        => _classifier = classifier;

    public IReadOnlyList<ParagraphGroup> Group(
        IReadOnlyList<ParagraphDiffResult> paragraphs,
        ReadingStyle readingStyle,
        int minGroupWords)
    {
        if (paragraphs.Count == 0) return [];

        var rawGroups = MergeShortParagraphs(paragraphs, minGroupWords);
        var classified = rawGroups.Select(g => new
        {
            Paragraphs     = g,
            Classification = Classify(g)
        }).ToList();

        var minimumTier = MinimumTier(readingStyle);

        // Determine which groups should show diff
        var anyMeetsThreshold = classified.Any(g =>
            g.Classification.HasValue &&
            g.Classification != ChangeClassification.New &&
            g.Classification >= minimumTier);

        if (anyMeetsThreshold)
        {
            return classified.Select(g => new ParagraphGroup(
                g.Paragraphs,
                g.Classification,
                ShowDiff: g.Classification.HasValue &&
                          g.Classification != ChangeClassification.New &&
                          g.Classification >= minimumTier)).ToList();
        }

        // Fallback: no group meets threshold — show groups at highest available classification,
        // but only when the highest available is Polish or above (Trivial-only scenes are never
        // flagged on the dashboard for StoryReader+, so the fallback is never meaningful there).
        var highestAvailable = classified
            .Where(g => g.Classification.HasValue && g.Classification != ChangeClassification.New)
            .Select(g => g.Classification!.Value)
            .DefaultIfEmpty()
            .Max();

        if (highestAvailable <= ChangeClassification.Trivial)
            return classified.Select(g => new ParagraphGroup(g.Paragraphs, g.Classification, ShowDiff: false)).ToList();

        return classified.Select(g => new ParagraphGroup(
            g.Paragraphs,
            g.Classification,
            ShowDiff: g.Classification.HasValue &&
                      g.Classification != ChangeClassification.New &&
                      g.Classification == highestAvailable)).ToList();
    }

    private ChangeClassification? Classify(IReadOnlyList<ParagraphDiffResult> group)
        => _classifier.Classify(group);

    private static List<List<ParagraphDiffResult>> MergeShortParagraphs(
        IReadOnlyList<ParagraphDiffResult> paragraphs, int minWords)
    {
        var groups  = new List<List<ParagraphDiffResult>>();
        var current = new List<ParagraphDiffResult>();
        int words   = 0;

        foreach (var para in paragraphs)
        {
            current.Add(para);
            words += para.TotalWords + para.WordsRemoved;

            if (words >= minWords)
            {
                groups.Add(current);
                current = [];
                words   = 0;
            }
        }

        if (current.Count > 0)
        {
            if (groups.Count > 0)
                groups[^1].AddRange(current); // merge trailing short group into the last
            else
                groups.Add(current);
        }

        return groups;
    }

    private static ChangeClassification MinimumTier(ReadingStyle style) => style switch
    {
        ReadingStyle.BetaReader    => ChangeClassification.Trivial,
        ReadingStyle.StoryReader   => ChangeClassification.Polish,
        ReadingStyle.AlphaReader   => ChangeClassification.Revision,
        ReadingStyle.StructureOnly => ChangeClassification.Rewrite,
        _                          => ChangeClassification.Polish
    };
}
