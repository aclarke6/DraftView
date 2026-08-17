using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Entities;

public sealed class AccessRequest
{
    public Guid Id { get; private set; }
    public Guid ReaderId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string? CoverNote { get; private set; }
    public string? ContactEmail { get; private set; }
    public AccessRequestStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public DateTime? SeenByReaderAt { get; private set; }

    private AccessRequest() { }

    public static AccessRequest Create(
        Guid readerId,
        Guid projectId,
        string? coverNote,
        string? contactEmail)
    {
        if (readerId == Guid.Empty)
            throw new InvariantViolationException("I-AR-01",
                "ReaderId must not be empty.");
        if (projectId == Guid.Empty)
            throw new InvariantViolationException("I-AR-02",
                "ProjectId must not be empty.");
        if (coverNote is not null && coverNote.Length > 500)
            throw new InvariantViolationException("I-AR-03",
                "Cover note must not exceed 500 characters.");

        return new AccessRequest
        {
            Id           = Guid.NewGuid(),
            ReaderId     = readerId,
            ProjectId    = projectId,
            CoverNote    = coverNote,
            ContactEmail = contactEmail,
            Status       = AccessRequestStatus.Pending,
            RequestedAt  = DateTime.UtcNow
        };
    }

    public void Approve()
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvariantViolationException("I-AR-04",
                "Only a pending request can be approved.");

        Status      = AccessRequestStatus.Approved;
        RespondedAt = DateTime.UtcNow;
    }

    public void Decline()
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvariantViolationException("I-AR-05",
                "Only a pending request can be declined.");

        Status      = AccessRequestStatus.Declined;
        RespondedAt = DateTime.UtcNow;
    }

    public void MarkSeenByReader()
    {
        if (Status != AccessRequestStatus.Declined)
            throw new InvariantViolationException("I-AR-06",
                "Only a declined request can be marked as seen.");

        if (SeenByReaderAt is null)
            SeenByReaderAt = DateTime.UtcNow;
    }

    public bool IsVisibleOnReaderDashboard(DateTime now)
    {
        if (Status == AccessRequestStatus.Pending) return true;
        if (Status == AccessRequestStatus.Approved) return false;

        // Declined: visible until the calendar day after SeenByReaderAt
        if (SeenByReaderAt is null) return true;
        return SeenByReaderAt.Value.Date >= now.Date;
    }
}
