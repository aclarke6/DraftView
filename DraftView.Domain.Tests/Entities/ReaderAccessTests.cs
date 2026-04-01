using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

public class ReaderAccessTests
{
    private static readonly Guid ValidReaderId  = Guid.NewGuid();
    private static readonly Guid ValidAuthorId  = Guid.NewGuid();
    private static readonly Guid ValidProjectId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Grant
    // ---------------------------------------------------------------------------

    [Fact]
    public void Grant_WithValidIds_ReturnsActiveAccess()
    {
        var before = DateTime.UtcNow;
        var access = ReaderAccess.Grant(ValidReaderId, ValidAuthorId, ValidProjectId);

        Assert.NotEqual(Guid.Empty, access.Id);
        Assert.Equal(ValidReaderId,  access.ReaderId);
        Assert.Equal(ValidAuthorId,  access.AuthorId);
        Assert.Equal(ValidProjectId, access.ProjectId);
        Assert.True(access.GrantedAt >= before);
        Assert.Null(access.RevokedAt);
        Assert.True(access.IsActive);
    }

    [Fact]
    public void Grant_WithEmptyReaderId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => ReaderAccess.Grant(Guid.Empty, ValidAuthorId, ValidProjectId));

        Assert.Equal("I-RA-READER", ex.InvariantCode);
    }

    [Fact]
    public void Grant_WithEmptyAuthorId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => ReaderAccess.Grant(ValidReaderId, Guid.Empty, ValidProjectId));

        Assert.Equal("I-RA-AUTHOR", ex.InvariantCode);
    }

    [Fact]
    public void Grant_WithEmptyProjectId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => ReaderAccess.Grant(ValidReaderId, ValidAuthorId, Guid.Empty));

        Assert.Equal("I-RA-PROJECT", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // Revoke
    // ---------------------------------------------------------------------------

    [Fact]
    public void Revoke_SetsRevokedAtAndIsActiveIsFalse()
    {
        var access = ReaderAccess.Grant(ValidReaderId, ValidAuthorId, ValidProjectId);
        var before = DateTime.UtcNow;

        access.Revoke();

        Assert.False(access.IsActive);
        Assert.NotNull(access.RevokedAt);
        Assert.True(access.RevokedAt >= before);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_DoesNotChangeRevokedAt()
    {
        var access = ReaderAccess.Grant(ValidReaderId, ValidAuthorId, ValidProjectId);
        access.Revoke();
        var firstRevocation = access.RevokedAt;

        access.Revoke();

        Assert.Equal(firstRevocation, access.RevokedAt);
    }

    // ---------------------------------------------------------------------------
    // Reinstate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reinstate_ClearsRevokedAtAndIsActiveIsTrue()
    {
        var access = ReaderAccess.Grant(ValidReaderId, ValidAuthorId, ValidProjectId);
        access.Revoke();

        access.Reinstate();

        Assert.True(access.IsActive);
        Assert.Null(access.RevokedAt);
    }

    [Fact]
    public void Reinstate_WhenNotRevoked_DoesNotThrow()
    {
        var access = ReaderAccess.Grant(ValidReaderId, ValidAuthorId, ValidProjectId);

        var ex = Record.Exception(() => access.Reinstate());

        Assert.Null(ex);
    }
}
