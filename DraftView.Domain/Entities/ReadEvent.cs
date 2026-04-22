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
    public int? LastReadVersionNumber { get; private set; }
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
}
