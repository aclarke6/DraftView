using DraftView.Infrastructure.Parsing;

namespace DraftView.Infrastructure.Tests.Parsing;

/// <summary>
/// TDD tests for RTF features found in the vault scan of The Fractured Lattice.
/// Each test describes the DESIRED output behaviour. Tests marked with a comment
/// may fail until the converter is updated to handle that feature.
/// </summary>
public class RtfConverterFeatureTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    private static Task<DraftView.Domain.Interfaces.Services.RtfConversionResult?> Convert(string uuid) =>
        new RtfConverter().ConvertAsync(FixturePath, uuid);

    // -------------------------------------------------------------------------
    // Em dash  (\emdash) - 891 hits in vault
    // Expected: RtfPipe converts \emdash to the unicode em dash character U+2014
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmDash_IsConvertedToUnicodeCharacter()
    {
        var result = await Convert("FEAT-EMDASH");

        Assert.NotNull(result);
        Assert.Contains("\u2014", result!.Html); // --
        Assert.DoesNotContain(@"\emdash", result.Html);
    }

    [Fact]
    public async Task EmDash_SurroundingTextIsPreserved()
    {
        var result = await Convert("FEAT-EMDASH");

        Assert.NotNull(result);
        Assert.Contains("footsteps", result!.Html);
        Assert.Contains("too late",  result.Html);
    }

    // -------------------------------------------------------------------------
    // En dash  (\endash) - 73 hits in vault
    // Expected: RtfPipe converts \endash to U+2013
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnDash_IsConvertedToUnicodeCharacter()
    {
        var result = await Convert("FEAT-ENDASH");

        Assert.NotNull(result);
        Assert.Contains("\u2013", result!.Html); // -
        Assert.DoesNotContain(@"\endash", result.Html);
    }

    // -------------------------------------------------------------------------
    // Non-breaking space (\~) - 36 hits in vault
    // Expected: becomes either the NBSP unicode char U+00A0 or &nbsp; entity
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NonBreakingSpace_IsConvertedAndNotLiteralBackslashTilde()
    {
        var result = await Convert("FEAT-NBSP");

        Assert.NotNull(result);
        Assert.DoesNotContain(@"\~", result!.Html);
    }

    // -------------------------------------------------------------------------
    // Embedded image (\pict) - 13 hits in vault
    // Expected: image is dropped cleanly - no broken tags, no crash,
    //           surrounding text is preserved
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Image_DoesNotCauseException()
    {
        var exception = await Record.ExceptionAsync(() => Convert("FEAT-IMAGE"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Image_SurroundingTextIsPreserved()
    {
        var result = await Convert("FEAT-IMAGE");

        Assert.NotNull(result);
        Assert.Contains("Before the image", result!.Html);
        Assert.Contains("After the image",  result.Html);
    }

    [Fact]
    public async Task Image_DoesNotLeaveRawRtfInOutput()
    {
        var result = await Convert("FEAT-IMAGE");

        Assert.NotNull(result);
        Assert.DoesNotContain(@"\pict",    result!.Html);
        Assert.DoesNotContain("pngblip",   result.Html);
        Assert.DoesNotContain("ffd8ffe0",  result.Html);
    }

    // -------------------------------------------------------------------------
    // Table (\trowd) - 325 hits in vault (research/character docs)
    // Expected: no crash, surrounding text preserved, no raw RTF leaking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Table_DoesNotCauseException()
    {
        var exception = await Record.ExceptionAsync(() => Convert("FEAT-TABLE"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Table_SurroundingTextIsPreserved()
    {
        var result = await Convert("FEAT-TABLE");

        Assert.NotNull(result);
        Assert.Contains("Before table", result!.Html);
        Assert.Contains("After table",  result.Html);
    }

    [Fact]
    public async Task Table_CellTextIsPresent()
    {
        var result = await Convert("FEAT-TABLE");

        Assert.NotNull(result);
        Assert.Contains("Cell one", result!.Html);
        Assert.Contains("Cell two", result.Html);
    }

    // -------------------------------------------------------------------------
    // List (\listid) - 728 hits in vault
    // Expected: no crash, list item text preserved
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_DoesNotCauseException()
    {
        var exception = await Record.ExceptionAsync(() => Convert("FEAT-LIST"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task List_ItemTextIsPreserved()
    {
        var result = await Convert("FEAT-LIST");

        Assert.NotNull(result);
        Assert.Contains("First item",  result!.Html);
        Assert.Contains("Second item", result.Html);
        Assert.Contains("Third item",  result.Html);
    }

    // -------------------------------------------------------------------------
    // Hyperlink (\fldinst HYPERLINK) - 54 hits in vault (one research doc)
    // Expected: no crash, link text or URL present, no raw RTF
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Hyperlink_DoesNotCauseException()
    {
        var exception = await Record.ExceptionAsync(() => Convert("FEAT-HYPERLINK"));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Hyperlink_SurroundingTextIsPreserved()
    {
        var result = await Convert("FEAT-HYPERLINK");

        Assert.NotNull(result);
        Assert.Contains("for details", result!.Html);
    }

    [Fact]
    public async Task Hyperlink_DoesNotLeaveRawRtfInOutput()
    {
        var result = await Convert("FEAT-HYPERLINK");

        Assert.NotNull(result);
        Assert.DoesNotContain(@"\fldinst", result!.Html);
        Assert.DoesNotContain(@"\fldrslt", result.Html);
    }

    // -------------------------------------------------------------------------
    // Compile placeholders - 6 hits in vault (research/template docs)
    // Expected: stripped from output - should not appear in reader HTML
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompilePlaceholder_ProjectTitle_IsStrippedFromOutput()
    {
        var result = await Convert("FEAT-COMPILE-TAG");

        Assert.NotNull(result);
        Assert.DoesNotContain("<$PROJECTTITLE>", result!.Html);
        Assert.DoesNotContain("<$fullname>",      result.Html);
        Assert.DoesNotContain("<$wc100>",         result.Html);
    }

    [Fact]
    public async Task CompilePlaceholder_SurroundingTextIsPreserved()
    {
        var result = await Convert("FEAT-COMPILE-TAG");

        Assert.NotNull(result);
        Assert.Contains("Word count", result!.Html);
        Assert.Contains("words",      result.Html);
    }

    // -------------------------------------------------------------------------
    // Character style index 0 (real vault uses ::0)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CharacterStyleIndexZero_TagIsStripped()
    {
        var result = await Convert("FEAT-CS-INDEX0");

        Assert.NotNull(result);
        Assert.DoesNotContain("<$Scr_Cs::0>",  result!.Html);
        Assert.DoesNotContain("</$Scr_Cs::0>", result.Html);
    }

    [Fact]
    public async Task CharacterStyleIndexZero_TextIsPreserved()
    {
        var result = await Convert("FEAT-CS-INDEX0");

        Assert.NotNull(result);
        Assert.Contains("A styled phrase", result!.Html);
    }

    // -------------------------------------------------------------------------
    // Paragraph styles with multiple indices (::0 and ::1 in real vault)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ParagraphStyleMultipleIndices_AllTagsStripped()
    {
        var result = await Convert("FEAT-PS-MULTI");

        Assert.NotNull(result);
        Assert.DoesNotContain("<$Scr_Ps::0>",  result!.Html);
        Assert.DoesNotContain("<$Scr_Ps::1>",  result.Html);
        Assert.DoesNotContain("<!$Scr_Ps::0>", result.Html);
        Assert.DoesNotContain("<!$Scr_Ps::1>", result.Html);
    }

    [Fact]
    public async Task ParagraphStyleMultipleIndices_TextIsPreserved()
    {
        var result = await Convert("FEAT-PS-MULTI");

        Assert.NotNull(result);
        Assert.Contains("Block one content", result!.Html);
        Assert.Contains("Block two content", result.Html);
        Assert.Contains("Before",            result.Html);
        Assert.Contains("After",             result.Html);
    }
}
