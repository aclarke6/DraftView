using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Application.Tests.Services;

public class ParagraphGroupingServiceTests
{
    private static readonly ParagraphGroupingService Sut = new();

    private static ParagraphDiffResult Para(DiffResultType type, int totalWords, int wordsAdded = 0, int wordsRemoved = 0)
        => new("text", "<p>text</p>", type, wordsAdded, wordsRemoved, totalWords);

    private static ParagraphDiffResult Unchanged(int words) => Para(DiffResultType.Unchanged, words);
    private static ParagraphDiffResult Modified(int words, int added, int removed) => Para(DiffResultType.Modified, words, added, removed);
    private static ParagraphDiffResult Added(int words) => Para(DiffResultType.Added, words, words, 0);
    private static ParagraphDiffResult Removed(int words) => Para(DiffResultType.Removed, 0, 0, words);

    // ---------------------------------------------------------------------------
    // Empty / no changes
    // ---------------------------------------------------------------------------

    [Fact]
    public void Group_EmptyList_ReturnsEmpty()
    {
        var result = Sut.Group([], ReadingStyle.StoryReader, 50);
        Assert.Empty(result);
    }

    [Fact]
    public void Group_AllUnchanged_ReturnsGroupsWithShowDiffFalse()
    {
        var paras = new[] { Unchanged(60), Unchanged(60) };
        var result = Sut.Group(paras, ReadingStyle.StoryReader, 50);
        Assert.All(result, g => Assert.False(g.ShowDiff));
    }

    // ---------------------------------------------------------------------------
    // Grouping — short paragraphs merged
    // ---------------------------------------------------------------------------

    [Fact]
    public void Group_ShortParagraphsMergedUntilMinWords()
    {
        // 20+20+20 = 60 words → one group
        var paras = new[] { Unchanged(20), Unchanged(20), Unchanged(20) };
        var result = Sut.Group(paras, ReadingStyle.StoryReader, 50);
        Assert.Single(result);
        Assert.Equal(3, result[0].Paragraphs.Count);
    }

    [Fact]
    public void Group_ParagraphExceedsMinWords_StandsAlone()
    {
        var paras = new[] { Unchanged(60), Unchanged(60) };
        var result = Sut.Group(paras, ReadingStyle.StoryReader, 50);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Paragraphs);
        Assert.Single(result[1].Paragraphs);
    }

    // ---------------------------------------------------------------------------
    // Threshold filtering — StoryReader = Polish+
    // ---------------------------------------------------------------------------

    [Fact]
    public void Group_GroupBelowThreshold_ShowDiffFalse()
    {
        // 1 word changed out of 100 = 1% = Trivial < Polish threshold
        var paras = new[] { Modified(100, 1, 1) };
        var result = Sut.Group(paras, ReadingStyle.StoryReader, 50);
        Assert.Single(result);
        Assert.False(result[0].ShowDiff);
    }

    [Fact]
    public void Group_GroupMeetsThreshold_ShowDiffTrue()
    {
        // 15 words changed out of 100 = 15% = Revision >= Polish threshold
        var paras = new[] { Modified(100, 15, 15) };
        var result = Sut.Group(paras, ReadingStyle.StoryReader, 50);
        Assert.Single(result);
        Assert.True(result[0].ShowDiff);
    }

    // ---------------------------------------------------------------------------
    // Fallback rule — no group meets threshold → show highest available level
    // ---------------------------------------------------------------------------

    [Fact]
    public void Group_NoGroupMeetsThreshold_ShowsDiffAtHighestAvailableLevel()
    {
        // Reader = Rewrite threshold. All groups are Polish (4 words changed / 100 = 4% = Polish).
        // Fallback: show Polish groups; skip Unchanged.
        var paras = new[]
        {
            Modified(100, 4, 4),   // 8/100 = 8% = Polish
            Modified(100, 4, 4),   // 8/100 = 8% = Polish
            Unchanged(100)
        };
        var result = Sut.Group(paras, ReadingStyle.StructureOnly, 50);

        var shown = result.Where(g => g.ShowDiff).ToList();
        Assert.Equal(2, shown.Count);
        Assert.All(shown, g => Assert.Equal(ChangeClassification.Polish, g.Classification));
    }

    [Fact]
    public void Group_SomeGroupsMeetThreshold_FallbackNotApplied()
    {
        // Reader = Revision. One group is Rewrite, one is Polish (4 words / 100 = 8% = Polish).
        // Rewrite meets threshold → fallback should NOT kick in → only Rewrite shown.
        var paras = new[]
        {
            Modified(100, 50, 50),  // 100/100 = 100% = Rewrite
            Modified(100, 4, 4)     // 8/100 = 8% = Polish
        };
        var result = Sut.Group(paras, ReadingStyle.AlphaReader, 50);

        var shown = result.Where(g => g.ShowDiff).ToList();
        Assert.Single(shown);
        Assert.Equal(ChangeClassification.Rewrite, shown[0].Classification);
    }

    // ---------------------------------------------------------------------------
    // Mixed: unchanged paragraphs always ShowDiff = false
    // ---------------------------------------------------------------------------

    [Fact]
    public void Group_UnchangedGroupsNeverShowDiff()
    {
        var paras = new[] { Unchanged(100) };
        var result = Sut.Group(paras, ReadingStyle.BetaReader, 50);
        Assert.Single(result);
        Assert.False(result[0].ShowDiff);
    }

    // ---------------------------------------------------------------------------
    // Added / Removed paragraphs classified correctly
    // ---------------------------------------------------------------------------

    [Fact]
    public void Group_AddedParagraph_ClassifiedAndShownForBetaReader()
    {
        // 60 words added = entirely new paragraph → Rewrite level (>40%)
        var paras = new[] { Added(60) };
        var result = Sut.Group(paras, ReadingStyle.BetaReader, 50);
        Assert.Single(result);
        Assert.True(result[0].ShowDiff);
    }
}
