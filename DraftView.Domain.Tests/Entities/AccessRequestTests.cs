using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class AccessRequestTests
{
    private static readonly Guid ValidReaderId  = Guid.NewGuid();
    private static readonly Guid ValidProjectId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_SetsAllProperties()
    {
        var before = DateTime.UtcNow;

        var req = AccessRequest.Create(
            ValidReaderId,
            ValidProjectId,
            "I love this genre.",
            "reader@example.com");

        Assert.NotEqual(Guid.Empty, req.Id);
        Assert.Equal(ValidReaderId, req.ReaderId);
        Assert.Equal(ValidProjectId, req.ProjectId);
        Assert.Equal("I love this genre.", req.CoverNote);
        Assert.Equal("reader@example.com", req.ContactEmail);
        Assert.Equal(AccessRequestStatus.Pending, req.Status);
        Assert.True(req.RequestedAt >= before);
        Assert.Null(req.RespondedAt);
        Assert.Null(req.SeenByReaderAt);
    }

    [Fact]
    public void Create_WithNullCoverNoteAndContactEmail_Succeeds()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);

        Assert.Null(req.CoverNote);
        Assert.Null(req.ContactEmail);
        Assert.Equal(AccessRequestStatus.Pending, req.Status);
    }

    [Fact]
    public void Create_Throws_WhenReaderIdIsEmpty()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            AccessRequest.Create(Guid.Empty, ValidProjectId, null, null));

        Assert.Equal("I-AR-01", ex.InvariantCode);
    }

    [Fact]
    public void Create_Throws_WhenProjectIdIsEmpty()
    {
        var ex = Assert.Throws<InvariantViolationException>(() =>
            AccessRequest.Create(ValidReaderId, Guid.Empty, null, null));

        Assert.Equal("I-AR-02", ex.InvariantCode);
    }

    [Fact]
    public void Create_Throws_WhenCoverNoteExceedsMaxLength()
    {
        var longNote = new string('a', 501);

        var ex = Assert.Throws<InvariantViolationException>(() =>
            AccessRequest.Create(ValidReaderId, ValidProjectId, longNote, null));

        Assert.Equal("I-AR-03", ex.InvariantCode);
    }

    [Fact]
    public void Create_AllowsCoverNoteAtExactlyMaxLength()
    {
        var maxNote = new string('a', 500);

        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, maxNote, null);

        Assert.Equal(maxNote, req.CoverNote);
    }

    // ---------------------------------------------------------------------------
    // Approve
    // ---------------------------------------------------------------------------

    [Fact]
    public void Approve_SetsStatusApprovedAndRecordsRespondedAt()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        var before = DateTime.UtcNow;

        req.Approve();

        Assert.Equal(AccessRequestStatus.Approved, req.Status);
        Assert.NotNull(req.RespondedAt);
        Assert.True(req.RespondedAt >= before);
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_Throws()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Approve();

        var ex = Assert.Throws<InvariantViolationException>(() => req.Approve());

        Assert.Equal("I-AR-04", ex.InvariantCode);
    }

    [Fact]
    public void Approve_WhenDeclined_Throws()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();

        var ex = Assert.Throws<InvariantViolationException>(() => req.Approve());

        Assert.Equal("I-AR-04", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Decline
    // ---------------------------------------------------------------------------

    [Fact]
    public void Decline_SetsStatusDeclinedAndRecordsRespondedAt()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        var before = DateTime.UtcNow;

        req.Decline();

        Assert.Equal(AccessRequestStatus.Declined, req.Status);
        Assert.NotNull(req.RespondedAt);
        Assert.True(req.RespondedAt >= before);
    }

    [Fact]
    public void Decline_WhenAlreadyDeclined_Throws()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();

        var ex = Assert.Throws<InvariantViolationException>(() => req.Decline());

        Assert.Equal("I-AR-05", ex.InvariantCode);
    }

    [Fact]
    public void Decline_WhenApproved_Throws()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Approve();

        var ex = Assert.Throws<InvariantViolationException>(() => req.Decline());

        Assert.Equal("I-AR-05", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // MarkSeenByReader
    // ---------------------------------------------------------------------------

    [Fact]
    public void MarkSeenByReader_OnDeclinedRequest_SetsSeenByReaderAt()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();
        var before = DateTime.UtcNow;

        req.MarkSeenByReader();

        Assert.NotNull(req.SeenByReaderAt);
        Assert.True(req.SeenByReaderAt >= before);
    }

    [Fact]
    public void MarkSeenByReader_WhenAlreadySeen_DoesNotUpdate()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();
        req.MarkSeenByReader();
        var firstSeen = req.SeenByReaderAt;

        req.MarkSeenByReader();

        Assert.Equal(firstSeen, req.SeenByReaderAt);
    }

    [Fact]
    public void MarkSeenByReader_OnPendingRequest_Throws()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);

        var ex = Assert.Throws<InvariantViolationException>(() => req.MarkSeenByReader());

        Assert.Equal("I-AR-06", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // IsVisibleOnReaderDashboard
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsVisibleOnReaderDashboard_WhenPending_ReturnsTrue()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);

        Assert.True(req.IsVisibleOnReaderDashboard(DateTime.UtcNow));
    }

    [Fact]
    public void IsVisibleOnReaderDashboard_WhenDeclinedAndNotSeen_ReturnsTrue()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();

        Assert.True(req.IsVisibleOnReaderDashboard(DateTime.UtcNow));
    }

    [Fact]
    public void IsVisibleOnReaderDashboard_WhenDeclinedAndSeenToday_ReturnsTrue()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();
        req.MarkSeenByReader();
        var today = DateTime.UtcNow;

        Assert.True(req.IsVisibleOnReaderDashboard(today));
    }

    [Fact]
    public void IsVisibleOnReaderDashboard_WhenDeclinedAndSeenYesterday_ReturnsFalse()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Decline();
        req.MarkSeenByReader();
        var tomorrow = DateTime.UtcNow.AddDays(1);

        Assert.False(req.IsVisibleOnReaderDashboard(tomorrow));
    }

    [Fact]
    public void IsVisibleOnReaderDashboard_WhenApproved_ReturnsFalse()
    {
        var req = AccessRequest.Create(ValidReaderId, ValidProjectId, null, null);
        req.Approve();

        Assert.False(req.IsVisibleOnReaderDashboard(DateTime.UtcNow));
    }
}
