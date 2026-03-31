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
}
