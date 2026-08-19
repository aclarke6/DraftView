using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for the TenancyMembership entity.
/// Covers: factory method invariants, role assignment, soft-delete, restore.
/// Excludes: tenancy limit enforcement (MT-Sprint-2), EF persistence.
/// </summary>
public class TenancyMembershipTests
{
    private static readonly Guid ValidTenancyId = Guid.NewGuid();
    private static readonly Guid ValidAccountId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsMembership()
    {
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.BetaReader);

        Assert.NotEqual(Guid.Empty, membership.Id);
        Assert.Equal(ValidTenancyId, membership.TenancyId);
        Assert.Equal(ValidAccountId, membership.AccountId);
        Assert.Equal(TenancyRole.BetaReader, membership.Role);
        Assert.False(membership.IsSoftDeleted);
        Assert.Null(membership.SoftDeletedAt);
    }

    [Fact]
    public void Create_WithAuthorRole_SetsRoleCorrectly()
    {
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.Author);

        Assert.Equal(TenancyRole.Author, membership.Role);
    }

    [Fact]
    public void Create_SetsJoinedAtToNow()
    {
        var before = DateTime.UtcNow;
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.BetaReader);

        Assert.True(membership.JoinedAt >= before);
        Assert.True(membership.JoinedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyTenancyId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => TenancyMembership.Create(Guid.Empty, ValidAccountId, TenancyRole.BetaReader));

        Assert.Equal("I-TMEM-TENANCY", ex.InvariantCode);
    }

    [Fact]
    public void Create_WithEmptyAccountId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => TenancyMembership.Create(ValidTenancyId, Guid.Empty, TenancyRole.BetaReader));

        Assert.Equal("I-TMEM-ACCOUNT", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.BetaReader);
        var before = DateTime.UtcNow;

        membership.SoftDelete();

        Assert.True(membership.IsSoftDeleted);
        Assert.NotNull(membership.SoftDeletedAt);
        Assert.True(membership.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.BetaReader);
        membership.SoftDelete();
        var firstDeletion = membership.SoftDeletedAt;

        membership.SoftDelete();

        Assert.Equal(firstDeletion, membership.SoftDeletedAt);
    }

    // ---------------------------------------------------------------------------
    // Restore
    // ---------------------------------------------------------------------------

    [Fact]
    public void Restore_AfterSoftDelete_ClearsSoftDeleteFlag()
    {
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.BetaReader);
        membership.SoftDelete();

        membership.Restore();

        Assert.False(membership.IsSoftDeleted);
        Assert.Null(membership.SoftDeletedAt);
    }

    [Fact]
    public void Restore_WhenNotDeleted_LeavesIsNotSoftDeletedState()
    {
        var membership = TenancyMembership.Create(ValidTenancyId, ValidAccountId, TenancyRole.BetaReader);

        membership.Restore();

        Assert.False(membership.IsSoftDeleted);
        Assert.Null(membership.SoftDeletedAt);
    }
}
