using ScrivenerSync.Domain.Enumerations;
using ScrivenerSync.Domain.Exceptions;

namespace ScrivenerSync.Domain.Entities;

public sealed class UserNotificationPreferences
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    // Beta reader preferences
    public bool NotifyOnNewSection { get; private set; }
    public bool NotifyOnSectionChanged { get; private set; }
    public NotifyOnReply NotifyOnReply { get; private set; }

    // Author-only preferences
    public AuthorDigestMode? AuthorDigestMode { get; private set; }
    public int? AuthorDigestIntervalHours { get; private set; }
    public string? AuthorTimezone { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private UserNotificationPreferences() { }

    // ---------------------------------------------------------------------------
    // Factories
    // ---------------------------------------------------------------------------

    public static UserNotificationPreferences CreateForBetaReader(Guid userId)
    {
        return new UserNotificationPreferences
        {
            Id                      = Guid.NewGuid(),
            UserId                  = userId,
            NotifyOnNewSection      = true,
            NotifyOnSectionChanged  = false,
            NotifyOnReply           = NotifyOnReply.AuthorOnly,
            AuthorDigestMode        = null,
            AuthorDigestIntervalHours = null,
            AuthorTimezone          = null
        };
    }

    public static UserNotificationPreferences CreateForAuthor(
        Guid userId,
        AuthorDigestMode digestMode,
        int? digestIntervalHours,
        string? timezone)
    {
        ValidateDigestSettings(digestMode, digestIntervalHours);

        return new UserNotificationPreferences
        {
            Id                        = Guid.NewGuid(),
            UserId                    = userId,
            NotifyOnNewSection        = false,
            NotifyOnSectionChanged    = false,
            NotifyOnReply             = NotifyOnReply.Never,
            AuthorDigestMode          = digestMode,
            AuthorDigestIntervalHours = digestMode == Enumerations.AuthorDigestMode.Digest ? digestIntervalHours : null,
            AuthorTimezone            = timezone
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
