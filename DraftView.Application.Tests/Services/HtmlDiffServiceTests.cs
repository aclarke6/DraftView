using DraftView.Application.Services;
using DraftView.Domain.Diff;
using DraftView.Domain.Enumerations;
using Xunit;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for HtmlDiffService.Compute.
/// Covers: null/empty handling, paragraph extraction, LCS comparison,
/// added/removed/unchanged classification, HTML preservation.
/// Excludes: UI rendering (Web layer), change classification (V-Sprint 4).
/// </summary>
public class HtmlDiffServiceTests
{
    private readonly HtmlDiffService _sut = new();

    [Fact]
    public void Compute_BothNull_ReturnsEmptyList()
    {
        var result = _sut.Compute(null, null);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_BothEmpty_ReturnsEmptyList()
    {
        var result = _sut.Compute(string.Empty, string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Compute_FromNull_AllAdded()
    {
        var to = "<p>Hello</p>";

        var result = _sut.Compute(null, to);

        Assert.Single(result);
        Assert.Equal(DiffResultType.Added, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
    }

    [Fact]
    public void Compute_ToNull_AllRemoved()
    {
        var from = "<p>Hello</p>";

        var result = _sut.Compute(from, null);

        Assert.Single(result);
        Assert.Equal(DiffResultType.Removed, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
    }

    [Fact]
    public void Compute_IdenticalContent_AllUnchanged()
    {
        var from = "<p>Hello</p>";
        var to = "<p>Hello</p>";

        var result = _sut.Compute(from, to);

        Assert.Single(result);
        Assert.Equal(DiffResultType.Unchanged, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
    }

    [Fact]
    public void Compute_AddedParagraph_DetectsAddition()
    {
        var from = "<p>Hello</p>";
        var to = "<p>Hello</p><p>World</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(2, result.Count);
        Assert.Equal(DiffResultType.Unchanged, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
        Assert.Equal(DiffResultType.Added, result[1].Type);
        Assert.Equal("World", result[1].Text);
    }

    [Fact]
    public void Compute_RemovedParagraph_DetectsRemoval()
    {
        var from = "<p>Hello</p><p>World</p>";
        var to = "<p>Hello</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(2, result.Count);
        Assert.Equal(DiffResultType.Unchanged, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
        Assert.Equal(DiffResultType.Removed, result[1].Type);
        Assert.Equal("World", result[1].Text);
    }

    [Fact]
    public void Compute_SingleChangedParagraph_ProducesModified()
    {
        var from = "<p>Hello world</p>";
        var to = "<p>Hello earth</p>";

        var result = _sut.Compute(from, to);

        Assert.Single(result);
        Assert.Equal(DiffResultType.Modified, result[0].Type);
    }

    [Fact]
    public void Compute_ModifiedParagraph_HtmlContainsDelForRemovedWord()
    {
        var from = "<p>Hello world</p>";
        var to = "<p>Hello earth</p>";

        var result = _sut.Compute(from, to);

        Assert.Contains("<del>world</del>", result[0].Html);
    }

    [Fact]
    public void Compute_ModifiedParagraph_HtmlContainsInsForInsertedWord()
    {
        var from = "<p>Hello world</p>";
        var to = "<p>Hello earth</p>";

        var result = _sut.Compute(from, to);

        Assert.Contains("<ins>earth</ins>", result[0].Html);
    }

    [Fact]
    public void Compute_ModifiedParagraph_PopulatesWordCounts()
    {
        var from = "<p>Hello world</p>";
        var to = "<p>Hello earth</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(1, result[0].WordsAdded);
        Assert.Equal(1, result[0].WordsRemoved);
    }

    [Fact]
    public void Compute_UnchangedParagraph_HasZeroWordChanges()
    {
        var from = "<p>Hello world</p>";
        var to = "<p>Hello world</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(0, result[0].WordsAdded);
        Assert.Equal(0, result[0].WordsRemoved);
    }

    [Fact]
    public void Compute_UnchangedParagraph_HasTotalWordsPopulated()
    {
        var from = "<p>Hello world today</p>";
        var to = "<p>Hello world today</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(3, result[0].TotalWords);
    }

    [Fact]
    public void Compute_AddedParagraph_HasWordsAddedEqualToWordCount()
    {
        var from = "<p>Hello</p>";
        var to = "<p>Hello</p><p>Brand new paragraph</p>";

        var result = _sut.Compute(from, to);

        var added = result.Single(r => r.Type == DiffResultType.Added);
        Assert.Equal(3, added.WordsAdded);
        Assert.Equal(0, added.WordsRemoved);
        Assert.Equal(3, added.TotalWords);
    }

    [Fact]
    public void Compute_RemovedParagraph_HasWordsRemovedEqualToWordCount()
    {
        var from = "<p>Hello</p><p>Gone forever now</p>";
        var to = "<p>Hello</p>";

        var result = _sut.Compute(from, to);

        var removed = result.Single(r => r.Type == DiffResultType.Removed);
        Assert.Equal(3, removed.WordsRemoved);
        Assert.Equal(0, removed.WordsAdded);
        Assert.Equal(0, removed.TotalWords);
    }

    [Fact]
    public void Compute_MismatchedChangedParagraphCounts_KeepsSeparateRemovedAndAdded()
    {
        // 2 removed, 1 added — cannot pair 1:1, keep as separate Removed/Added
        var from = "<p>Para one</p><p>Para two</p>";
        var to = "<p>Para three</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Count(r => r.Type == DiffResultType.Removed));
        Assert.Equal(1, result.Count(r => r.Type == DiffResultType.Added));
    }

    [Fact]
    public void Compute_MultiParagraph_CorrectSequence()
    {
        // B replaced by D: 1 Removed + 1 Added -> merged to Modified
        var from = "<p>A</p><p>B</p><p>C</p>";
        var to = "<p>A</p><p>D</p><p>C</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(3, result.Count);
        Assert.Equal(DiffResultType.Unchanged, result[0].Type);
        Assert.Equal("A", result[0].Text);
        Assert.Equal(DiffResultType.Modified, result[1].Type);
        Assert.Equal(DiffResultType.Unchanged, result[2].Type);
        Assert.Equal("C", result[2].Text);
    }

    [Fact]
    public void Compute_IgnoresHtmlTagDifferences_WhenTextIsIdentical()
    {
        var from = "<p><strong>Hello</strong></p>";
        var to = "<p>Hello</p>";

        var result = _sut.Compute(from, to);

        Assert.Single(result);
        Assert.Equal(DiffResultType.Unchanged, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
    }

    [Fact]
    public void Compute_PurelyAddedParagraph_PreservesOriginalHtml()
    {
        // A second paragraph added with no removed counterpart preserves its original HTML.
        var from = "<p>Hello</p>";
        var to = "<p>Hello</p><p><em>World</em></p>";

        var result = _sut.Compute(from, to);

        var added = result.Single(r => r.Type == DiffResultType.Added);
        Assert.Contains("<em>World</em>", added.Html);
    }

    [Fact]
    public void Compute_ModifiedParagraph_HtmlContainsDiffSpansNotOriginalInlineTags()
    {
        // A modified paragraph produces diff HTML (del/ins), not the original inline markup.
        var from = "<p><strong>Hello</strong></p>";
        var to = "<p><em>World</em></p>";

        var result = _sut.Compute(from, to);

        Assert.Single(result);
        Assert.Equal(DiffResultType.Modified, result[0].Type);
        Assert.Contains("<del>", result[0].Html);
        Assert.Contains("<ins>", result[0].Html);
    }
}
