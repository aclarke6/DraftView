using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class ReadEvent
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime FirstOpenedAt { get; private set; }
    public DateTime LastOpenedAt { get; private set; }
    public int OpenCount { get; private set; }

    /// <summary>
    /// True when the reader has read the current version of this scene
    /// (time threshold met or manually marked). False until then.
    /// </summary>
    public bool IsRead { get; private set; }

    public int? LastReadVersionNumber { get; private set; }

    /// <summary>
    /// The version number read immediately before the current LastReadVersionNumber.
    /// Stored so that MarkAsUnread can restore the previous diff baseline.
    /// </summary>
    public int? PreviousReadVersionNumber { get; private set; }

    /// <summary>
    /// The timestamp at which the reader last marked this section as read.
    /// Null until first mark-as-read. Used to enforce the reader's diff cooldown setting.
    /// </summary>
    public DateTimeOffset? LastMarkedReadAt { get; private set; }

    public Guid? ResumeAnchorId { get; private set; }

    /// <summary>
    /// The version number at which the reader dismissed the update banner.
    /// When this equals the current version number, the banner is not shown.
    /// Null until the reader has dismissed the banner for the first time.
    /// </summary>
    public int? BannerDismissedAtVersion { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private ReadEvent() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public static ReadEvent Create(Guid sectionId, Guid userId)
    {
        var now = DateTime.UtcNow;

        return new ReadEvent
        {
            Id            = Guid.NewGuid(),
            SectionId     = sectionId,
            UserId        = userId,
            FirstOpenedAt = now,
            LastOpenedAt  = now,
            OpenCount     = 1
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    public void RecordOpen()
    {
        // I-12: FirstOpenedAt is never modified after creation
        LastOpenedAt = DateTime.UtcNow;
        OpenCount++;
    }

    /// <summary>
    /// Records the version number most recently read by this reader.
    /// Called when a reader opens a section that has a current SectionVersion.
    /// </summary>
    /// <param name="versionNumber">The version number (must be >= 1).</param>
    /// <exception cref="InvariantViolationException">Thrown when version number is less than 1.</exception>
    public void UpdateLastReadVersion(int versionNumber)
    {
        if (versionNumber < 1)
            throw new InvariantViolationException("I-READ-VER",
                "Version number must be 1 or greater.");

        LastReadVersionNumber = versionNumber;
    }

    /// <summary>
    /// Records that the reader dismissed the update banner at the given version.
    /// Subsequent opens of the same version will not show the banner.
    /// </summary>
    /// <param name="versionNumber">The version number being dismissed (must be >= 1).</param>
    /// <exception cref="InvariantViolationException">Thrown when version number is less than 1.</exception>
    public void DismissBannerAtVersion(int versionNumber)
    {
        if (versionNumber < 1)
            throw new InvariantViolationException("I-READ-BANNER",
                "Version number must be 1 or greater.");

        BannerDismissedAtVersion = versionNumber;
    }

    /// <summary>
    /// Records the passage anchor that represents the latest resume position.
    /// </summary>
    /// <param name="resumeAnchorId">The passage anchor id to use for resume.</param>
    /// <exception cref="InvariantViolationException">Thrown when the anchor id is empty.</exception>
    public void UpdateResumeAnchor(Guid resumeAnchorId)
    {
        if (resumeAnchorId == Guid.Empty)
            throw new InvariantViolationException("I-READ-ANCHOR",
                "Resume anchor id must not be empty.");

        ResumeAnchorId = resumeAnchorId;
    }

    public void ClearResumeAnchor()
    {
        ResumeAnchorId = null;
    }

    /// <summary>
    /// Records that the reader has read the section at the given version.
    /// Stores the previous LastReadVersionNumber so MarkAsUnread can restore it.
    /// </summary>
    /// <param name="versionNumber">The version number being marked as read (must be >= 1).</param>
    /// <exception cref="InvariantViolationException">Thrown when version number is less than 1.</exception>
    public void MarkAsRead(int versionNumber)
    {
        if (versionNumber < 1)
            throw new InvariantViolationException("I-READ-MARK",
                "Version number must be 1 or greater.");

        PreviousReadVersionNumber = LastReadVersionNumber;
        LastReadVersionNumber     = versionNumber;
        LastMarkedReadAt          = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reverses the last MarkAsRead, restoring the previous diff baseline.
    /// Clears LastMarkedReadAt so the cooldown does not suppress the diff.
    /// </summary>
    public void MarkAsUnread()
    {
        IsRead = false;
        (LastReadVersionNumber, PreviousReadVersionNumber) = (PreviousReadVersionNumber, LastReadVersionNumber);
        LastMarkedReadAt = null;
    }

    /// <summary>
    /// Records that the reader has read the current content of this scene.
    /// Sets IsRead = true and captures LastMarkedReadAt for cooldown enforcement.
    /// Called when the time threshold is met or the reader manually marks as read.
    /// </summary>
    public void MarkRead()
    {
        IsRead           = true;
        LastMarkedReadAt = DateTimeOffset.UtcNow;
    }
}
