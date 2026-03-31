using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class UserInvitationAcceptanceTests
{
    [Fact]
    public void AcceptInvitation_WithValidDisplayName_UpdatesDisplayName_AndActivatesUser()
    {
        var user = User.Create("reader@example.com", "Pending", Role.BetaReader);

        user.AcceptInvitation("Reader Four");

        Assert.Equal("Reader Four", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.NotNull(user.ActivatedAt);
    }

    [Fact]
    public void AcceptInvitation_TrimsDisplayName()
    {
        var user = User.Create("reader@example.com", "Pending", Role.BetaReader);

        user.AcceptInvitation("  Reader Four  ");

        Assert.Equal("Reader Four", user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AcceptInvitation_WithInvalidDisplayName_ThrowsInvariantViolationException(string? displayName)
    {
        var user = User.Create("reader@example.com", "Pending", Role.BetaReader);

#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(() => user.AcceptInvitation(displayName));
#pragma warning restore CS8604

        Assert.Equal("I-DISPLAYNAME", ex.InvariantCode);
    }

    [Fact]
    public void AcceptInvitation_WhenSoftDeleted_ThrowsInvariantViolationException()
    {
        var user = User.Create("reader@example.com", "Pending", Role.BetaReader);
        user.SoftDelete();

        var ex = Assert.Throws<InvariantViolationException>(() => user.AcceptInvitation("Reader Four"));

        Assert.Equal("I-USER-DELETED", ex.InvariantCode);
    }
}
