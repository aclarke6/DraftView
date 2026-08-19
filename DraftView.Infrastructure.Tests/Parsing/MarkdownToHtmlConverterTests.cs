using DraftView.Infrastructure.Parsing;

namespace DraftView.Infrastructure.Tests.Parsing;

/// <summary>
/// Tests for MarkdownToHtmlConverter.
/// Covers: empty input, heading levels (h1/h2/h3), plain paragraphs, inline bold,
/// inline italic, multiple blank-line-separated paragraphs, and bold/italic combination.
/// </summary>
public class MarkdownToHtmlConverterTests
{
    private readonly MarkdownToHtmlConverter _sut = new();

    [Fact]
    public void Convert_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Convert(string.Empty));
    }

    [Fact]
    public void Convert_NullEquivalent_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _sut.Convert(""));
    }

    [Fact]
    public void Convert_H1Heading_WrapsInH1()
    {
        var result = _sut.Convert("# Chapter One");
        Assert.Contains("<h1>Chapter One</h1>", result);
    }

    [Fact]
    public void Convert_H2Heading_WrapsInH2()
    {
        var result = _sut.Convert("## Scene One");
        Assert.Contains("<h2>Scene One</h2>", result);
    }

    [Fact]
    public void Convert_H3Heading_WrapsInH3()
    {
        var result = _sut.Convert("### Sub-heading");
        Assert.Contains("<h3>Sub-heading</h3>", result);
    }

    [Fact]
    public void Convert_PlainParagraph_WrapsInP()
    {
        var result = _sut.Convert("Once upon a time.");
        Assert.Contains("<p>Once upon a time.</p>", result);
    }

    [Fact]
    public void Convert_BoldText_ReplacesWithStrong()
    {
        var result = _sut.Convert("This is **bold** text.");
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public void Convert_ItalicText_ReplacesWithEm()
    {
        var result = _sut.Convert("This is *italic* text.");
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public void Convert_BoldDoesNotMatchDoubleAsterisksAsItalic()
    {
        var result = _sut.Convert("**bold**");
        Assert.DoesNotContain("<em>", result);
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public void Convert_MultipleParagraphs_EachWrappedSeparately()
    {
        var input = "First paragraph.\n\nSecond paragraph.";
        var result = _sut.Convert(input);
        Assert.Contains("<p>First paragraph.</p>", result);
        Assert.Contains("<p>Second paragraph.</p>", result);
    }

    [Fact]
    public void Convert_HeadingFollowedByParagraph_BothRendered()
    {
        var input = "# Title\n\nBody text.";
        var result = _sut.Convert(input);
        Assert.Contains("<h1>Title</h1>", result);
        Assert.Contains("<p>Body text.</p>", result);
    }

    [Fact]
    public void Convert_BoldAndItalicInSameParagraph_BothApplied()
    {
        var result = _sut.Convert("**bold** and *italic*.");
        Assert.Contains("<strong>bold</strong>", result);
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public void Convert_WindowsLineEndings_HandledAsBlankLineSeparator()
    {
        var input = "First.\r\n\r\nSecond.";
        var result = _sut.Convert(input);
        Assert.Contains("<p>First.</p>", result);
        Assert.Contains("<p>Second.</p>", result);
    }
}
