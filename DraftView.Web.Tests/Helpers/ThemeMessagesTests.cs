using DraftView.Web.Helpers;

namespace DraftView.Web.Tests.Helpers;

public class ThemeMessagesTests
{
    // -------------------------------------------------------------------------
    // 403 — Access Denied
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("")]
    [InlineData("UnknownTheme")]
    public void Get403_AdventureOrUnknown_ReturnsAdventureMessage(string theme)
    {
        var msg = ThemeMessages.Get403(theme);
        Assert.Contains("paths are closed", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CrimeLight")]
    [InlineData("CrimeDark")]
    public void Get403_Crime_ReturnsCrimeMessage(string theme)
    {
        var msg = ThemeMessages.Get403(theme);
        Assert.Contains("clearance", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NoirLight")]
    [InlineData("NoirDark")]
    public void Get403_Noir_ReturnsNoirMessage(string theme)
    {
        var msg = ThemeMessages.Get403(theme);
        Assert.Contains("credentials", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ContemporaryLight")]
    [InlineData("ContemporaryDark")]
    public void Get403_Contemporary_ReturnsContemporaryMessage(string theme)
    {
        var msg = ThemeMessages.Get403(theme);
        Assert.Contains("access", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("HistoricLight")]
    [InlineData("HistoricDark")]
    public void Get403_Historic_ReturnsHistoricMessage(string theme)
    {
        var msg = ThemeMessages.Get403(theme);
        Assert.Contains("chamber", msg, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // 404 — Not Found
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("")]
    [InlineData("UnknownTheme")]
    public void Get404_AdventureOrUnknown_ReturnsAdventureMessage(string theme)
    {
        var msg = ThemeMessages.Get404(theme);
        // The existing Adventure 404 message must be preserved exactly
        Assert.Equal("The trail you've chosen slips beyond the borders of any realm we can enter.", msg);
    }

    [Theory]
    [InlineData("CrimeLight")]
    [InlineData("CrimeDark")]
    public void Get404_Crime_ReturnsCrimeMessage(string theme)
    {
        var msg = ThemeMessages.Get404(theme);
        Assert.Contains("cold", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NoirLight")]
    [InlineData("NoirDark")]
    public void Get404_Noir_ReturnsNoirMessage(string theme)
    {
        var msg = ThemeMessages.Get404(theme);
        Assert.Contains("gone cold", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ContemporaryLight")]
    [InlineData("ContemporaryDark")]
    public void Get404_Contemporary_ReturnsContemporaryMessage(string theme)
    {
        var msg = ThemeMessages.Get404(theme);
        Assert.Contains("couldn't find", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("HistoricLight")]
    [InlineData("HistoricDark")]
    public void Get404_Historic_ReturnsHistoricMessage(string theme)
    {
        var msg = ThemeMessages.Get404(theme);
        Assert.Contains("archives", msg, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // 405 — Method Not Allowed
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("")]
    [InlineData("UnknownTheme")]
    public void Get405_AdventureOrUnknown_ReturnsAdventureMessage(string theme)
    {
        var msg = ThemeMessages.Get405(theme);
        Assert.Contains("passage", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CrimeLight")]
    [InlineData("CrimeDark")]
    public void Get405_Crime_ReturnsCrimeMessage(string theme)
    {
        var msg = ThemeMessages.Get405(theme);
        Assert.Contains("round here", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NoirLight")]
    [InlineData("NoirDark")]
    public void Get405_Noir_ReturnsNoirMessage(string theme)
    {
        var msg = ThemeMessages.Get405(theme);
        Assert.Contains("blown", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ContemporaryLight")]
    [InlineData("ContemporaryDark")]
    public void Get405_Contemporary_ReturnsContemporaryMessage(string theme)
    {
        var msg = ThemeMessages.Get405(theme);
        Assert.Contains("supported", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("HistoricLight")]
    [InlineData("HistoricDark")]
    public void Get405_Historic_ReturnsHistoricMessage(string theme)
    {
        var msg = ThemeMessages.Get405(theme);
        Assert.Contains("accommodate", msg, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // 500 — Internal Server Error
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("")]
    [InlineData("UnknownTheme")]
    public void Get500_AdventureOrUnknown_ReturnsAdventureMessage(string theme)
    {
        var msg = ThemeMessages.Get500(theme);
        Assert.Contains("spell", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CrimeLight")]
    [InlineData("CrimeDark")]
    public void Get500_Crime_ReturnsCrimeMessage(string theme)
    {
        var msg = ThemeMessages.Get500(theme);
        Assert.Contains("sideways", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("NoirLight")]
    [InlineData("NoirDark")]
    public void Get500_Noir_ReturnsNoirMessage(string theme)
    {
        var msg = ThemeMessages.Get500(theme);
        Assert.Contains("operation failed", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ContemporaryLight")]
    [InlineData("ContemporaryDark")]
    public void Get500_Contemporary_ReturnsContemporaryMessage(string theme)
    {
        var msg = ThemeMessages.Get500(theme);
        Assert.Contains("looking into it", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("HistoricLight")]
    [InlineData("HistoricDark")]
    public void Get500_Historic_ReturnsHistoricMessage(string theme)
    {
        var msg = ThemeMessages.Get500(theme);
        Assert.Contains("amiss", msg, StringComparison.OrdinalIgnoreCase);
    }
}
