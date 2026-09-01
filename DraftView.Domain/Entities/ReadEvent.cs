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

    /// <summary>
    /// The timestamp at which the reader last marked this section as read.
    /// Null until first mark-as-read. Used to enforce the reader's diff cooldown setting.
    /// </summary>
    public DateTimeOffset? LastMarkedReadAt { get; private set; }

    public Guid? ResumeAnchorId { get; private set; }

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
    /// Records that the reader has read the current content of this scene.
    /// Sets IsRead = true and captures LastMarkedReadAt for cooldown enforcement.
    /// Called when the time threshold is met or the reader manually marks as read.
    /// </summary>
    public void MarkRead()
    {
        IsRead           = true;
        LastMarkedReadAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the scene as unread. Clears IsRead and LastMarkedReadAt.
    /// </summary>
    public void MarkAsUnread()
    {
        IsRead           = false;
        LastMarkedReadAt = null;
    }

    /// <summary>
    /// Records the passage anchor that represents the latest resume position.
    /// </summary>
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
