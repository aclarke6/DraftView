using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ChangeClassificationService.Classify.
/// Covers word-level threshold classification: Trivial, Polish, Revision, Rewrite.
/// Trivial threshold: wordsChanged less than max(5, ceil(totalWords * 0.01)).
/// Polish: 1-10%. Revision: 10-40%. Rewrite: greater than 40%.
/// Excludes: diff generation (HtmlDiffServiceTests), persistence, UI rendering.
/// </summary>
public class ChangeClassificationServiceTests
{
    private readonly ChangeClassificationService _sut = new();

    // ---------------------------------------------------------------------------
    // Null / empty
    // ---------------------------------------------------------------------------

    [Fact]
    public void Classify_WithNullParagraphs_ReturnsNull()
    {
        var result = _sut.Classify(null!);

        Assert.Null(result);
    }

    [Fact]
    public void Classify_WithEmptyParagraphs_ReturnsNull()
    {
        var result = _sut.Classify(Array.Empty<ParagraphDiffResult>());

        Assert.Null(result);
    }

    [Fact]
    public void Classify_WithNoWordChanges_ReturnsNull()
    {
        // 200-word scene, nothing changed
        var paragraphs = new[]
        {
            Unchanged(totalWords: 200)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Trivial
    // ---------------------------------------------------------------------------

    [Fact]
    public void Classify_WithFourWordsChanged_SmallScene_ReturnsTrivial()
    {
        // Scene: 200 words. Threshold: max(5, ceil(2)) = 5. Changed: 4 < 5 -> Trivial.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 196),
            Modified(wordsAdded: 2, wordsRemoved: 2, totalWords: 198)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Trivial, result);
    }

    [Fact]
    public void Classify_WithEightWordsChanged_LargeScene_ReturnsTrivial()
    {
        // Scene: 1000 words. Threshold: max(5, ceil(10)) = 10. Changed: 8 < 10 -> Trivial.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 996),
            Modified(wordsAdded: 4, wordsRemoved: 4, totalWords: 1000)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Trivial, result);
    }

    // ---------------------------------------------------------------------------
    // Polish (1% to less than 10%)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Classify_WithPolishChanges_ReturnsPolish()
    {
        // Scene: 200 words. Changed: 10 words = 5%. 5% is in [1%, 10%) -> Polish.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 190),
            Modified(wordsAdded: 5, wordsRemoved: 5, totalWords: 200)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Polish, result);
    }

    [Fact]
    public void Classify_WithFiveWordsChanged_SmallScene_ReturnsPolish()
    {
        // Scene: 200 words. Trivial threshold = max(5, 2) = 5. Changed: 5 = exactly at threshold -> Polish (not Trivial).
        var paragraphs = new[]
        {
            Unchanged(totalWords: 195),
            Modified(wordsAdded: 2, wordsRemoved: 3, totalWords: 200)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Polish, result);
    }

    // ---------------------------------------------------------------------------
    // Revision (10% to less than 40%)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Classify_WithRevisionChanges_ReturnsRevision()
    {
        // Scene: 200 words. Changed: 50 words = 25% -> Revision.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 150),
            Modified(wordsAdded: 25, wordsRemoved: 25, totalWords: 200)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Revision, result);
    }

    [Fact]
    public void Classify_AtPolishRevisionBoundary_ReturnsRevision()
    {
        // Scene: 100 words total. Changed: 10 words = exactly 10% -> Revision.
        // No Unchanged paragraph so total = Modified.TotalWords only.
        var paragraphs = new[]
        {
            Modified(wordsAdded: 5, wordsRemoved: 5, totalWords: 100)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Revision, result);
    }

    // ---------------------------------------------------------------------------
    // Rewrite (40% or more)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Classify_WithRewriteChanges_ReturnsRewrite()
    {
        // Scene: 200 words. Changed: 120 words = 60% -> Rewrite.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 80),
            Modified(wordsAdded: 60, wordsRemoved: 60, totalWords: 200)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Rewrite, result);
    }

    [Fact]
    public void Classify_AtRevisionRewriteBoundary_ReturnsRewrite()
    {
        // Scene: 100 words total. Changed: 40 words = exactly 40% -> Rewrite.
        // No Unchanged paragraph so total = Modified.TotalWords only.
        var paragraphs = new[]
        {
            Modified(wordsAdded: 20, wordsRemoved: 20, totalWords: 100)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Rewrite, result);
    }

    [Fact]
    public void Classify_WithOnlyAddedParagraphs_ClassifiesFromWordCount()
    {
        // Entire new paragraph added: 50 words added to a 200-word scene = 25% -> Revision.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 200),
            Added(wordsAdded: 50)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Revision, result);
    }

    [Fact]
    public void Classify_WithOnlyRemovedParagraphs_ClassifiesFromWordCount()
    {
        // 80 words removed from a 200-word scene (now 120 words). 80/120 = 67% -> Rewrite.
        var paragraphs = new[]
        {
            Unchanged(totalWords: 120),
            Removed(wordsRemoved: 80)
        };

        var result = _sut.Classify(paragraphs);

        Assert.Equal(ChangeClassification.Rewrite, result);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ParagraphDiffResult Unchanged(int totalWords)
        => new("unchanged", "<p>unchanged</p>", DiffResultType.Unchanged,
               wordsAdded: 0, wordsRemoved: 0, totalWords: totalWords);

    private static ParagraphDiffResult Modified(int wordsAdded, int wordsRemoved, int totalWords)
        => new("modified", "<p>modified</p>", DiffResultType.Modified,
               wordsAdded: wordsAdded, wordsRemoved: wordsRemoved, totalWords: totalWords);

    private static ParagraphDiffResult Added(int wordsAdded)
        => new("added", "<p>added</p>", DiffResultType.Added,
               wordsAdded: wordsAdded, wordsRemoved: 0, totalWords: wordsAdded);

    private static ParagraphDiffResult Removed(int wordsRemoved)
        => new("removed", "<p>removed</p>", DiffResultType.Removed,
               wordsAdded: 0, wordsRemoved: wordsRemoved, totalWords: 0);
}
