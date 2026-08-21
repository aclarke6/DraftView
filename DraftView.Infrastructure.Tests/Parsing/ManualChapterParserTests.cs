using System.Text;
using DraftView.Domain.Exceptions;
using DraftView.Domain.Interfaces.Services;
using DraftView.Infrastructure.Parsing;

namespace DraftView.Infrastructure.Tests.Parsing;

/// <summary>
/// Tests for PlainTextChapterParser, DocxChapterParser, and ChapterFileParserResolver.
/// Covers: .txt content pass-through, .docx heading/bold/italic mapping, complex-element
/// stripping, parser selection by extension, case-insensitive extension matching,
/// and UnsupportedFileTypeException for unknown extensions.
/// Excludes: repository persistence, application service orchestration, file-size limits
/// (enforced in the application service, not in parsers).
/// </summary>
public class ManualChapterParserTests
{
    // ── PlainTextChapterParser ────────────────────────────────────────────────

    [Fact]
    public async Task PlainTextParser_ReturnsContentAsIs()
    {
        var parser = new PlainTextChapterParser();
        var input  = "# Chapter One\n\nOnce upon a time…";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var result = await parser.ParseAsync(stream);

        Assert.Equal(input, result);
    }

    [Fact]
    public async Task PlainTextParser_PreservesMarkdownSyntax()
    {
        var parser = new PlainTextChapterParser();
        var input  = "**bold** and *italic* text";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var result = await parser.ParseAsync(stream);

        Assert.Equal(input, result);
    }

    [Fact]
    public async Task PlainTextParser_SupportedExtension_IsTxt()
    {
        var parser = new PlainTextChapterParser();
        Assert.Equal(".txt", parser.SupportedExtension);
    }

    // ── DocxChapterParser ─────────────────────────────────────────────────────

    [Fact]
    public void DocxParser_SupportedExtension_IsDocx()
    {
        var parser = new DocxChapterParser();
        Assert.Equal(".docx", parser.SupportedExtension);
    }

    [Fact]
    public async Task DocxParser_ParsesHeadingStyles_ToMarkdown()
    {
        // Build a minimal docx in memory with Heading1, Heading2, Heading3
        var docx = DocxTestHelper.BuildDocx(new[]
        {
            ("Heading1", "Chapter One"),
            ("Heading2", "Scene One"),
            ("Heading3", "Sub-Scene"),
            ("Normal",   "Normal paragraph text.")
        });

        var parser = new DocxChapterParser();
        var result = await parser.ParseAsync(docx);

        Assert.Contains("# Chapter One",   result);
        Assert.Contains("## Scene One",    result);
        Assert.Contains("### Sub-Scene",   result);
        Assert.Contains("Normal paragraph text.", result);
    }

    [Fact]
    public async Task DocxParser_ParsesBoldRuns_ToMarkdown()
    {
        var docx = DocxTestHelper.BuildDocxWithFormattedRuns(new[]
        {
            (bold: true, italic: false, text: "strong"),
            (bold: false, italic: false, text: " normal")
        });

        var parser = new DocxChapterParser();
        var result = await parser.ParseAsync(docx);

        Assert.Contains("**strong**", result);
    }

    [Fact]
    public async Task DocxParser_ParsesItalicRuns_ToMarkdown()
    {
        var docx = DocxTestHelper.BuildDocxWithFormattedRuns(new[]
        {
            (bold: false, italic: true, text: "emphasis"),
            (bold: false, italic: false, text: " normal")
        });

        var parser = new DocxChapterParser();
        var result = await parser.ParseAsync(docx);

        Assert.Contains("*emphasis*", result);
    }

    [Fact]
    public async Task DocxParser_StripsComplexElements_Cleanly()
    {
        // A docx with a table; result should not contain raw XML or table marker text
        var docx = DocxTestHelper.BuildDocxWithTable("Cell A", "Cell B");

        var parser = new DocxChapterParser();
        var result = await parser.ParseAsync(docx);

        Assert.DoesNotContain("<w:", result);
        Assert.DoesNotContain("Cell A", result);
        Assert.DoesNotContain("Cell B", result);
    }

    // ── ChapterFileParserResolver ─────────────────────────────────────────────

    [Fact]
    public void Resolver_ResolvesTxtParser()
    {
        var resolver = BuildResolver();
        var parser   = resolver.Resolve(".txt");
        Assert.IsType<PlainTextChapterParser>(parser);
    }

    [Fact]
    public void Resolver_ResolvesDocxParser()
    {
        var resolver = BuildResolver();
        var parser   = resolver.Resolve(".docx");
        Assert.IsType<DocxChapterParser>(parser);
    }

    [Fact]
    public void Resolver_IsCaseInsensitive()
    {
        var resolver = BuildResolver();
        var parser   = resolver.Resolve(".TXT");
        Assert.IsType<PlainTextChapterParser>(parser);
    }

    [Fact]
    public void Resolver_ThrowsUnsupportedFileTypeException_ForUnknownExtension()
    {
        var resolver = BuildResolver();
        var ex = Assert.Throws<UnsupportedFileTypeException>(() => resolver.Resolve(".pdf"));
        Assert.Equal(".pdf", ex.Extension);
    }

    private static ChapterFileParserResolver BuildResolver() =>
        new ChapterFileParserResolver(new IChapterFileParser[]
        {
            new PlainTextChapterParser(),
            new DocxChapterParser()
        });
}
