using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for ChangeClassificationService.Classify.
/// Covers threshold-based classification from paragraph diffs.
/// Excludes: diff generation itself (covered in HtmlDiffService tests), persistence wiring, and UI rendering.
/// </summary>
public class ChangeClassificationServiceTests
{
    private readonly ChangeClassificationService _sut = new();

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
    public void Classify_WithNoChanges_ReturnsNull()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 10, added: 0, removed: 0));

        Assert.Null(result);
    }

    [Fact]
    public void Classify_WithMinorChanges_ReturnsPolish()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 9, added: 1, removed: 0));

        Assert.Equal(ChangeClassification.Polish, result);
    }

    [Fact]
    public void Classify_WithModerateChanges_ReturnsRevision()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 7, added: 2, removed: 1));

        Assert.Equal(ChangeClassification.Revision, result);
    }

    [Fact]
    public void Classify_WithMajorChanges_ReturnsRewrite()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 3, added: 4, removed: 3));

        Assert.Equal(ChangeClassification.Rewrite, result);
    }

    [Fact]
    public void Classify_AtExactRevisionThreshold_ReturnsRevision()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 8, added: 2, removed: 0));

        Assert.Equal(ChangeClassification.Revision, result);
    }

    [Fact]
    public void Classify_AtExactRewriteThreshold_ReturnsRewrite()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 4, added: 6, removed: 0));

        Assert.Equal(ChangeClassification.Rewrite, result);
    }

    [Fact]
    public void Classify_WithOnlyAdditions_ClassifiesCorrectly()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 8, added: 2, removed: 0));

        Assert.Equal(ChangeClassification.Revision, result);
    }

    [Fact]
    public void Classify_WithOnlyRemovals_ClassifiesCorrectly()
    {
        var result = _sut.Classify(CreateParagraphs(unchanged: 8, added: 0, removed: 2));

        Assert.Equal(ChangeClassification.Revision, result);
    }

    private static IReadOnlyList<ParagraphDiffResult> CreateParagraphs(int unchanged, int added, int removed)
    {
        var paragraphs = new List<ParagraphDiffResult>();

        for (var i = 0; i < unchanged; i++)
            paragraphs.Add(new ParagraphDiffResult($"unchanged-{i}", $"<p>unchanged-{i}</p>", DiffResultType.Unchanged));

        for (var i = 0; i < added; i++)
            paragraphs.Add(new ParagraphDiffResult($"added-{i}", $"<p>added-{i}</p>", DiffResultType.Added));

        for (var i = 0; i < removed; i++)
            paragraphs.Add(new ParagraphDiffResult($"removed-{i}", $"<p>removed-{i}</p>", DiffResultType.Removed));

        return paragraphs;
    }
}
