using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class ReaderAccess
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    public Guid Id { get; private set; }
    public Guid ReaderId { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid ProjectId { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    private ReaderAccess() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public static ReaderAccess Grant(Guid readerId, Guid authorId, Guid projectId)
    {
        if (readerId == Guid.Empty)
            throw new InvariantViolationException("I-RA-READER",
                "ReaderAccess must have a valid reader.");

        if (authorId == Guid.Empty)
            throw new InvariantViolationException("I-RA-AUTHOR",
                "ReaderAccess must have a valid author.");

        if (projectId == Guid.Empty)
            throw new InvariantViolationException("I-RA-PROJECT",
                "ReaderAccess must have a valid project.");

        return new ReaderAccess
        {
            Id        = Guid.NewGuid(),
            ReaderId  = readerId,
            AuthorId  = authorId,
            ProjectId = projectId,
            GrantedAt = DateTime.UtcNow
        };
    }

    // ---------------------------------------------------------------------------
    // Behaviour
    // ---------------------------------------------------------------------------

    public bool IsActive => RevokedAt is null;

    public void Revoke()
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTime.UtcNow;
    }

    public void Reinstate()
    {
        RevokedAt = null;
    }
}
