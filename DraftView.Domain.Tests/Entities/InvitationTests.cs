using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class InvitationTests
{
    private static User MakeBetaReader() =>
        User.Create("reader@example.com", "Test Reader", Role.BetaReader);

    // ---------------------------------------------------------------------------
    // Create - AlwaysOpen
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_AlwaysOpen_ReturnsInvitationWithCorrectState()
    {
        var user = MakeBetaReader();
        var before = DateTime.UtcNow;

        var invitation = Invitation.CreateAlwaysOpen(user.Id);

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(user.Id, invitation.UserId);
        Assert.Equal(ExpiryPolicy.AlwaysOpen, invitation.ExpiryPolicy);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.NotNull(invitation.Token);
        Assert.NotEmpty(invitation.Token);
        Assert.Null(invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedAt);
        Assert.Null(invitation.CancelledAt);
        Assert.True(invitation.IssuedAt >= before);
    }

    // ---------------------------------------------------------------------------
    // Create - ExpiresAt
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_ExpiresAt_ReturnsInvitationWithExpiry()
    {
        var user = MakeBetaReader();
        var expiry = DateTime.UtcNow.AddDays(7);

        var invitation = Invitation.CreateWithExpiry(user.Id, expiry);

        Assert.Equal(ExpiryPolicy.ExpiresAt, invitation.ExpiryPolicy);
        Assert.Equal(expiry, invitation.ExpiresAt);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Create_ExpiresAt_WithPastExpiry_ThrowsInvariantViolationException()
    {
        var user = MakeBetaReader();
        var pastExpiry = DateTime.UtcNow.AddDays(-1);

        var ex = Assert.Throws<InvariantViolationException>(
            () => Invitation.CreateWithExpiry(user.Id, pastExpiry));

        Assert.Equal("I-EXPIRY", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Accept
    // ---------------------------------------------------------------------------

    [Fact]
    public void Accept_WhenPending_SetsStatusAcceptedAndRecordsTimestamp()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        var before = DateTime.UtcNow;

        invitation.Accept();

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.NotNull(invitation.AcceptedAt);
        Assert.True(invitation.AcceptedAt >= before);
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_ThrowsInvariantViolationException()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.Accept();

        var ex = Assert.Throws<InvariantViolationException>(() => invitation.Accept());

        Assert.Equal("I-INVITE-STATE", ex.InvariantCode);
    }

    [Fact]
    public void Accept_WhenCancelled_ThrowsInvariantViolationException()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.Cancel();

        var ex = Assert.Throws<InvariantViolationException>(() => invitation.Accept());

        Assert.Equal("I-INVITE-STATE", ex.InvariantCode);
    }

    [Fact]
    public void Accept_WhenExpired_ThrowsInvariantViolationException()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.ForceExpire();

        var ex = Assert.Throws<InvariantViolationException>(() => invitation.Accept());

        Assert.Equal("I-INVITE-STATE", ex.InvariantCode);
    }

    [Fact]
    public void Accept_WhenPastExpiryDate_ThrowsInvariantViolationException()
    {
        var expiry = DateTime.UtcNow.AddDays(7);
        var invitation = Invitation.CreateWithExpiry(Guid.NewGuid(), expiry);
        invitation.ForceExpire();

        var ex = Assert.Throws<InvariantViolationException>(() => invitation.Accept());

        Assert.Equal("I-INVITE-STATE", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Cancel
    // ---------------------------------------------------------------------------

    [Fact]
    public void Cancel_WhenPending_SetsStatusCancelledAndRecordsTimestamp()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        var before = DateTime.UtcNow;

        invitation.Cancel();

        Assert.Equal(InvitationStatus.Cancelled, invitation.Status);
        Assert.NotNull(invitation.CancelledAt);
        Assert.True(invitation.CancelledAt >= before);
    }

    [Fact]
    public void Cancel_WhenAlreadyAccepted_ThrowsInvariantViolationException()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.Accept();

        var ex = Assert.Throws<InvariantViolationException>(() => invitation.Cancel());

        Assert.Equal("I-INVITE-STATE", ex.InvariantCode);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_DoesNotChangeTimestamp()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.Cancel();
        var firstCancellation = invitation.CancelledAt;

        invitation.Cancel();

        Assert.Equal(firstCancellation, invitation.CancelledAt);
    }

    // ---------------------------------------------------------------------------
    // IsValid
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsValid_WhenPendingAlwaysOpen_ReturnsTrue()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());

        Assert.True(invitation.IsValid());
    }

    [Fact]
    public void IsValid_WhenPendingNotExpired_ReturnsTrue()
    {
        var invitation = Invitation.CreateWithExpiry(Guid.NewGuid(), DateTime.UtcNow.AddDays(7));

        Assert.True(invitation.IsValid());
    }

    [Fact]
    public void IsValid_WhenCancelled_ReturnsFalse()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.Cancel();

        Assert.False(invitation.IsValid());
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        var invitation = Invitation.CreateAlwaysOpen(Guid.NewGuid());
        invitation.ForceExpire();

        Assert.False(invitation.IsValid());
    }
}
