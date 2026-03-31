using DraftView.Infrastructure.Parsing;
using DraftView.Domain.Interfaces.Services;

namespace DraftView.Infrastructure.Tests.Parsing;

public class RtfConverterTests
{
    private readonly string _testDataPath;

    public RtfConverterTests()
    {
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
    }

    private string ScrivPath => _testDataPath;

    // ---------------------------------------------------------------------------
    // GetContentPath
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetContentPath_ReturnsCorrectPath()
    {
        var converter = new RtfConverter();

        var path = converter.GetContentPath(ScrivPath, "SCEN-001");

        var expected = Path.Combine(ScrivPath, "Files", "Data", "SCEN-001", "content.rtf");
        Assert.Equal(expected, path);
    }

    // ---------------------------------------------------------------------------
    // ConvertAsync - file exists
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_WithValidRtf_ReturnsHtmlAndHash()
    {
        var converter = new RtfConverter();

        var result = await converter.ConvertAsync(ScrivPath, "SCEN-001");

        Assert.NotNull(result);
        Assert.NotNull(result!.Html);
        Assert.NotNull(result.Hash);
        Assert.NotEmpty(result.Html);
        Assert.NotEmpty(result.Hash);
    }

    [Fact]
    public async Task ConvertAsync_WithValidRtf_HtmlContainsParagraphTags()
    {
        var converter = new RtfConverter();

        var result = await converter.ConvertAsync(ScrivPath, "SCEN-001");

        Assert.NotNull(result);
        Assert.Contains("<p", result!.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertAsync_WithValidRtf_HtmlContainsExpectedText()
    {
        var converter = new RtfConverter();

        var result = await converter.ConvertAsync(ScrivPath, "SCEN-001");

        Assert.NotNull(result);
        Assert.Contains("Nothing moved", result!.Html);
    }

    [Fact]
    public async Task ConvertAsync_SameContent_ProducesSameHash()
    {
        var converter = new RtfConverter();

        var result1 = await converter.ConvertAsync(ScrivPath, "SCEN-001");
        var result2 = await converter.ConvertAsync(ScrivPath, "SCEN-001");

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1!.Hash, result2!.Hash);
    }

    // ---------------------------------------------------------------------------
    // ConvertAsync - file does not exist
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_WhenFileNotFound_ReturnsNull()
    {
        var converter = new RtfConverter();

        var result = await converter.ConvertAsync(ScrivPath, "SCEN-EMPTY");

        Assert.Null(result);
    }

    [Fact]
    public async Task ConvertAsync_WhenUuidNotFound_ReturnsNull()
    {
        var converter = new RtfConverter();

        var result = await converter.ConvertAsync(ScrivPath, "NONEXISTENT-UUID");

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Scrivener character style tag stripping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_ScrivenerCharStyleTags_AreNotPresentInOutput()
    {
        var converter = new RtfConverter();

        foreach (var uuid in new[] { "SCEN-001", "SCEN-002", "SCEN-003" })
        {
            var result = await converter.ConvertAsync(ScrivPath, uuid);
            if (result is null) continue;

            Assert.DoesNotContain("<$Scr_Cs::", result.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("</$Scr_Cs::", result.Html, StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------------------
    // Scrivener character style tag stripping (<$Scr_Cs::N>)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_WithScrCsTags_TagsAreAbsentFromOutput()
    {
        var result = await new RtfConverter().ConvertAsync(_testDataPath, "SCEN-CS-STYLE");

        Assert.NotNull(result);
        Assert.DoesNotContain("<$Scr_Cs::",  result!.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("</$Scr_Cs::", result.Html,  StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_WithScrCsTags_TextContentIsPreserved()
    {
        var result = await new RtfConverter().ConvertAsync(_testDataPath, "SCEN-CS-STYLE");

        Assert.NotNull(result);
        Assert.Contains("Shadows rise in the old city", result!.Html);
    }

    // ---------------------------------------------------------------------------
    // Scrivener paragraph style tag stripping (<$Scr_Ps::N>)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_WithScrPsTags_TagsAreAbsentFromOutput()
    {
        var result = await new RtfConverter().ConvertAsync(_testDataPath, "SCEN-PS-STYLE");

        Assert.NotNull(result);
        Assert.DoesNotContain("<$Scr_Ps::",  result!.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("</$Scr_Ps::", result.Html,  StringComparison.Ordinal);
        Assert.DoesNotContain("<!$Scr_Ps::", result.Html,  StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_WithScrPsTags_TextContentIsPreserved()
    {
        var result = await new RtfConverter().ConvertAsync(_testDataPath, "SCEN-PS-STYLE");

        Assert.NotNull(result);
        Assert.Contains("Issren Sarouk", result!.Html);
    }

    // ---------------------------------------------------------------------------
    // Both tag types present in the same document
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_WithBothTagTypes_AllTagsAreStripped()
    {
        var result = await new RtfConverter().ConvertAsync(_testDataPath, "SCEN-BOTH-STYLE");

        Assert.NotNull(result);
        Assert.DoesNotContain("<$Scr_Cs::",  result!.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("</$Scr_Cs::", result.Html,  StringComparison.Ordinal);
        Assert.DoesNotContain("<$Scr_Ps::",  result.Html,  StringComparison.Ordinal);
        Assert.DoesNotContain("</$Scr_Ps::", result.Html,  StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_WithBothTagTypes_TextContentIsPreserved()
    {
        var result = await new RtfConverter().ConvertAsync(_testDataPath, "SCEN-BOTH-STYLE");

        Assert.NotNull(result);
        Assert.Contains("Meet me at dawn", result!.Html);
    }
}
