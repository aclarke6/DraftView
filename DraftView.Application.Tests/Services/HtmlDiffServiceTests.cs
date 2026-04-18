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
    public void Compute_ChangedParagraph_DetectsRemovalAndAddition()
    {
        var from = "<p>Hello</p>";
        var to = "<p>World</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(2, result.Count);
        Assert.Equal(DiffResultType.Removed, result[0].Type);
        Assert.Equal("Hello", result[0].Text);
        Assert.Equal(DiffResultType.Added, result[1].Type);
        Assert.Equal("World", result[1].Text);
    }

    [Fact]
    public void Compute_MultiParagraph_CorrectSequence()
    {
        var from = "<p>A</p><p>B</p><p>C</p>";
        var to = "<p>A</p><p>D</p><p>C</p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(4, result.Count);
        Assert.Equal(DiffResultType.Unchanged, result[0].Type);
        Assert.Equal("A", result[0].Text);
        Assert.Equal(DiffResultType.Removed, result[1].Type);
        Assert.Equal("B", result[1].Text);
        Assert.Equal(DiffResultType.Added, result[2].Type);
        Assert.Equal("D", result[2].Text);
        Assert.Equal(DiffResultType.Unchanged, result[3].Type);
        Assert.Equal("C", result[3].Text);
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
    public void Compute_PreservesOriginalHtml_InResult()
    {
        var from = "<p><strong>Hello</strong></p>";
        var to = "<p><em>World</em></p>";

        var result = _sut.Compute(from, to);

        Assert.Equal(2, result.Count);
        Assert.Contains("<strong>Hello</strong>", result[0].Html);
        Assert.Contains("<em>World</em>", result[1].Html);
    }
}
