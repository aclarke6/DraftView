using DraftView.Infrastructure.Parsing;

namespace DraftView.Infrastructure.Tests.Parsing;

public class ScrivenerStylesParserTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    // ---------------------------------------------------------------------------
    // Missing file
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_WhenStylesFileAbsent_ReturnsEmptyDictionary()
    {
        var result = ScrivenerStylesParser.Parse(@"C:\NonExistent\Fake.scriv");
        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // Fixture styles.xml
    // ---------------------------------------------------------------------------

    [Fact]
    public void Parse_FixtureVault_ReturnsThreeStyles()
    {
        var result = ScrivenerStylesParser.Parse(FixturePath);
        Assert.Equal(3, result.Count);
    }

    [Theory]
    [InlineData(1, "Dialogue",        "character", "scr-style-dialogue")]
    [InlineData(2, "Block Quote",     "paragraph", "scr-style-block-quote")]
    [InlineData(3, "Foreign Language","character", "scr-style-foreign-language")]
    public void Parse_FixtureVault_StylePropertiesAreCorrect(
        int id, string name, string type, string cssClass)
    {
        var result = ScrivenerStylesParser.Parse(FixturePath);

        Assert.True(result.ContainsKey(id), $"Style ID {id} not found.");
        var style = result[id];
        Assert.Equal(name,     style.Name);
        Assert.Equal(type,     style.Type);
        Assert.Equal(cssClass, style.CssClassName);
    }

    [Fact]
    public void Parse_FixtureVault_AllCssClassNamesMatchSafePattern()
    {
        var result = ScrivenerStylesParser.Parse(FixturePath);
        foreach (var style in result.Values)
        {
            Assert.StartsWith("scr-style-", style.CssClassName);
            Assert.Matches(@"^[a-z0-9\-]+$", style.CssClassName);
        }
    }
}
