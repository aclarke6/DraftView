using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class UserPreferencesTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // CreateForBetaReader
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateForBetaReader_ReturnsPrefsWithSensibleDefaults()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        Assert.NotEqual(Guid.Empty, prefs.Id);
        Assert.Equal(UserId, prefs.UserId);
        Assert.True(prefs.NotifyOnNewSection);
        Assert.False(prefs.NotifyOnSectionChanged);
        Assert.Equal(NotifyOnReply.AuthorOnly, prefs.NotifyOnReply);
        Assert.Null(prefs.AuthorDigestMode);
        Assert.Null(prefs.AuthorDigestIntervalHours);
        Assert.Null(prefs.AuthorTimezone);
        Assert.Equal(DisplayTheme.Light, prefs.DisplayTheme);
    }

    // ---------------------------------------------------------------------------
    // CreateForAuthor
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateForAuthor_ReturnsPrefsWithAuthorFields()
    {
        var prefs = UserPreferences.CreateForAuthor(UserId, AuthorDigestMode.Immediate, null, "Europe/London");

        Assert.Equal(UserId, prefs.UserId);
        Assert.Equal(AuthorDigestMode.Immediate, prefs.AuthorDigestMode);
        Assert.Null(prefs.AuthorDigestIntervalHours);
        Assert.Equal("Europe/London", prefs.AuthorTimezone);
    }

    [Fact]
    public void CreateForAuthor_DigestMode_RequiresInterval()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => UserPreferences.CreateForAuthor(UserId, AuthorDigestMode.Digest, null, "Europe/London"));

        Assert.Equal("I-19-INTERVAL", ex.InvariantCode);
    }

    [Fact]
    public void CreateForAuthor_DigestMode_WithInterval_Succeeds()
    {
        var prefs = UserPreferences.CreateForAuthor(UserId, AuthorDigestMode.Digest, 4, "Europe/London");

        Assert.Equal(4, prefs.AuthorDigestIntervalHours);
    }

    // ---------------------------------------------------------------------------
    // UpdateBetaReaderPreferences
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateBetaReaderPreferences_UpdatesAllFields()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        prefs.UpdateBetaReaderPreferences(false, true, NotifyOnReply.AnyParticipant);

        Assert.False(prefs.NotifyOnNewSection);
        Assert.True(prefs.NotifyOnSectionChanged);
        Assert.Equal(NotifyOnReply.AnyParticipant, prefs.NotifyOnReply);
    }

    // ---------------------------------------------------------------------------
    // Prose font defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateForBetaReader_DefaultsToSystemSerifAndMedium()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        Assert.Equal(ProseFont.SystemSerif, prefs.ProseFont);
        Assert.Equal(ProseFontSize.Medium, prefs.ProseFontSize);
    }

    [Fact]
    public void CreateForAuthor_DefaultsToSystemSerifAndMedium()
    {
        var prefs = UserPreferences.CreateForAuthor(UserId, AuthorDigestMode.Immediate, null, "Europe/London");

        Assert.Equal(ProseFont.SystemSerif, prefs.ProseFont);
        Assert.Equal(ProseFontSize.Medium, prefs.ProseFontSize);
    }

    // ---------------------------------------------------------------------------
    // UpdateProseFontPreferences
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateProseFontPreferences_UpdatesBothFields()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        prefs.UpdateProseFontPreferences(ProseFont.Humanist, ProseFontSize.Large);

        Assert.Equal(ProseFont.Humanist, prefs.ProseFont);
        Assert.Equal(ProseFontSize.Large, prefs.ProseFontSize);
    }

    [Theory]
    [InlineData(ProseFont.SystemSerif)]
    [InlineData(ProseFont.Humanist)]
    [InlineData(ProseFont.Classic)]
    [InlineData(ProseFont.SansSerif)]
    public void UpdateProseFontPreferences_CanSetEveryFontValue(ProseFont font)
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        prefs.UpdateProseFontPreferences(font, ProseFontSize.Medium);

        Assert.Equal(font, prefs.ProseFont);
    }

    [Theory]
    [InlineData(ProseFontSize.Small)]
    [InlineData(ProseFontSize.Medium)]
    [InlineData(ProseFontSize.Large)]
    [InlineData(ProseFontSize.ExtraLarge)]
    public void UpdateProseFontPreferences_CanSetEverySizeValue(ProseFontSize size)
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        prefs.UpdateProseFontPreferences(ProseFont.SystemSerif, size);

        Assert.Equal(size, prefs.ProseFontSize);
    }

    [Fact]
    public void UpdateProseFontPreferences_CalledTwice_LastValueWins()
    {
        var prefs = UserPreferences.CreateForBetaReader(UserId);

        prefs.UpdateProseFontPreferences(ProseFont.Classic, ProseFontSize.Small);
        prefs.UpdateProseFontPreferences(ProseFont.SansSerif, ProseFontSize.ExtraLarge);

        Assert.Equal(ProseFont.SansSerif, prefs.ProseFont);
        Assert.Equal(ProseFontSize.ExtraLarge, prefs.ProseFontSize);
    }

    // ---------------------------------------------------------------------------
    // UpdateAuthorPreferences
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateAuthorPreferences_ImmediateMode_ClearsInterval()
    {
        var prefs = UserPreferences.CreateForAuthor(UserId, AuthorDigestMode.Digest, 4, "Europe/London");

        prefs.UpdateAuthorPreferences(AuthorDigestMode.Immediate, null, "Europe/London");

        Assert.Equal(AuthorDigestMode.Immediate, prefs.AuthorDigestMode);
        Assert.Null(prefs.AuthorDigestIntervalHours);
    }

    [Fact]
    public void UpdateAuthorPreferences_DigestMode_WithoutInterval_Throws()
    {
        var prefs = UserPreferences.CreateForAuthor(UserId, AuthorDigestMode.Immediate, null, "Europe/London");

        var ex = Assert.Throws<InvariantViolationException>(
            () => prefs.UpdateAuthorPreferences(AuthorDigestMode.Digest, null, "Europe/London"));

        Assert.Equal("I-19-INTERVAL", ex.InvariantCode);
    }
}
