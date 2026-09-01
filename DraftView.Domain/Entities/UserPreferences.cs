using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class UserPreferences
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id {get; private set;}
    public Guid UserId{get; private set;}

    // Global UI preferences
    public DisplayTheme DisplayTheme{get; private set;}

    // Beta reader preferences
    public bool NotifyOnNewSection{get; private set;}
    public bool NotifyOnSectionChanged{get; private set;}
    public NotifyOnReply NotifyOnReply{get; private set;}

    // Author preferences
    public AuthorDigestMode? AuthorDigestMode{get; private set;}
    public int? AuthorDigestIntervalHours{get; private set;}
    public string? AuthorTimezone{get; private set;}

    // Reader prose preferences
    public ProseFont ProseFont{get; private set;}
    public ProseFontSize ProseFontSize{get; private set;}

    // Reader discovery profile (all optional)
    public string? ReaderBio{get; private set;}
    public string? ReaderGenreInterests{get; private set;}
    public ReaderPace? ReaderPace{get; private set;}

    // Reader diff preferences
    public bool ShowDiffOnRevisit{get; private set;}
    public ReadingStyle ReadingStyle{get; private set;}
    public int DiffCooldownHours{get; private set;}
    public bool ShowEdits{get; private set;}



    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private UserPreferences() { }

    // ---------------------------------------------------------------------------
    // Factories
    // ---------------------------------------------------------------------------

    public static UserPreferences CreateForBetaReader(Guid userId)
    {
        return new UserPreferences {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotifyOnNewSection = true,
            NotifyOnSectionChanged = false,
            NotifyOnReply = NotifyOnReply.AuthorOnly,
            AuthorDigestMode = null,
            AuthorDigestIntervalHours = null,
            AuthorTimezone = null,
            DisplayTheme = DisplayTheme.Light,
            ProseFont = ProseFont.SystemSerif,
            ProseFontSize = ProseFontSize.Medium,
            ShowDiffOnRevisit = false,
            ReadingStyle = ReadingStyle.StoryReader,
            DiffCooldownHours = 24,
            ShowEdits = false
        };
    }

    public static UserPreferences CreateForAuthor(
        Guid userId,
        AuthorDigestMode digestMode,
        int? digestIntervalHours,
        string? timezone)
    {
        ValidateDigestSettings(digestMode, digestIntervalHours);

        return new UserPreferences {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotifyOnNewSection = false,
            NotifyOnSectionChanged = false,
            NotifyOnReply = NotifyOnReply.Never,
            AuthorDigestMode = digestMode,
            AuthorDigestIntervalHours = digestMode == Enumerations.AuthorDigestMode.Digest ? digestIntervalHours : null,
            AuthorTimezone = timezone,
            DisplayTheme = DisplayTheme.Light,
            ProseFont = ProseFont.SystemSerif,
            ProseFontSize = ProseFontSize.Medium,
            ShowDiffOnRevisit = false,
            ReadingStyle = ReadingStyle.StoryReader,
            DiffCooldownHours = 24,
            ShowEdits = false
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    public void UpdateBetaReaderPreferences(
        bool notifyOnNewSection,
        bool notifyOnSectionChanged,
        NotifyOnReply notifyOnReply)
    {
        NotifyOnNewSection     = notifyOnNewSection;
        NotifyOnSectionChanged = notifyOnSectionChanged;
        NotifyOnReply          = notifyOnReply;
    }

    public void UpdateAuthorPreferences(
        AuthorDigestMode digestMode,
        int? digestIntervalHours,
        string? timezone)
    {
        ValidateDigestSettings(digestMode, digestIntervalHours);

        AuthorDigestMode          = digestMode;
        AuthorDigestIntervalHours = digestMode == Enumerations.AuthorDigestMode.Digest ? digestIntervalHours : null;
        AuthorTimezone            = timezone;
    }

    public void UpdateDisplayTheme(DisplayTheme displayTheme)
    {
        DisplayTheme = displayTheme;
    }

    public void UpdateProseFontPreferences(ProseFont proseFont, ProseFontSize proseFontSize)
    {
        ProseFont = proseFont;
        ProseFontSize = proseFontSize;
    }

    public void UpdateReaderProfile(string? bio, string? genreInterests, ReaderPace? pace)
    {
        ReaderBio            = bio;
        ReaderGenreInterests = genreInterests;
        ReaderPace           = pace;
    }

    /// <summary>
    /// Updates the reader's diff-on-revisit preferences.
    /// </summary>
    /// <param name="showDiffOnRevisit">Whether to show a diff when revisiting an updated chapter.</param>
    /// <param name="readingStyle">The minimum change classification the reader wants to see.</param>
    /// <param name="diffCooldownHours">Hours to wait after marking as read before showing new diffs. Must be >= 1.</param>
    /// <exception cref="InvariantViolationException">Thrown when diffCooldownHours is less than 1.</exception>
    public void UpdateDiffPreferences(bool showDiffOnRevisit, ReadingStyle readingStyle, int diffCooldownHours)
    {
        if (diffCooldownHours < 1)
            throw new InvariantViolationException("I-PREF-COOLDOWN",
                "Diff cooldown must be at least 1 hour.");

        ShowDiffOnRevisit = showDiffOnRevisit;
        ReadingStyle      = readingStyle;
        DiffCooldownHours = diffCooldownHours;
    }

    public void UpdateShowEdits(bool showEdits)
    {
        ShowEdits = showEdits;
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static void ValidateDigestSettings(AuthorDigestMode digestMode, int? digestIntervalHours)
    {
        if (digestMode == Enumerations.AuthorDigestMode.Digest && (digestIntervalHours == null || digestIntervalHours <= 0))
            throw new InvariantViolationException("I-19-INTERVAL",
                "A digest interval in hours is required when digest mode is Digest.");
    }
}
