using DraftView.Domain.Entities;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for the Tenancy entity.
/// Covers: factory method invariants, default property values, name update, soft-delete.
/// Excludes: billing/subscription enforcement (MT-Sprint-2), EF persistence.
/// </summary>
public class TenancyTests
{
    private static readonly Guid ValidOwnerAccountId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsTenancy()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "My Novel Workspace");

        Assert.NotEqual(Guid.Empty, tenancy.Id);
        Assert.Equal(ValidOwnerAccountId, tenancy.OwnerAccountId);
        Assert.Equal("My Novel Workspace", tenancy.Name);
        Assert.True(tenancy.IsActive);
        Assert.False(tenancy.IsSoftDeleted);
        Assert.Null(tenancy.SoftDeletedAt);
    }

    [Fact]
    public void Create_SetsDefaultMaxBetaReaderCountToFive()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Workspace");

        Assert.Equal(5, tenancy.MaxBetaReaderCount);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Workspace");

        Assert.True(tenancy.CreatedAt >= before);
        Assert.True(tenancy.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyOwnerAccountId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => Tenancy.Create(Guid.Empty, "Workspace"));

        Assert.Equal("I-TEN-OWNER", ex.InvariantCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsInvariantViolationException(string? name)
    {
#pragma warning disable CS8604
        var ex = Assert.Throws<InvariantViolationException>(
            () => Tenancy.Create(ValidOwnerAccountId, name));
#pragma warning restore CS8604

        Assert.Equal("I-TEN-NAME", ex.InvariantCode);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "  My Workspace  ");

        Assert.Equal("My Workspace", tenancy.Name);
    }

    // ---------------------------------------------------------------------------
    // UpdateName
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateName_WithValidName_SetsName()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Old Name");

        tenancy.UpdateName("New Name");

        Assert.Equal("New Name", tenancy.Name);
    }

    [Fact]
    public void UpdateName_TrimsWhitespace()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Old Name");

        tenancy.UpdateName("  New Name  ");

        Assert.Equal("New Name", tenancy.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateName_WithInvalidName_ThrowsInvariantViolationException(string? name)
    {
#pragma warning disable CS8604
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Old Name");
        var ex = Assert.Throws<InvariantViolationException>(() => tenancy.UpdateName(name));
#pragma warning restore CS8604

        Assert.Equal("I-TEN-NAME", ex.InvariantCode);
    }

    // ---------------------------------------------------------------------------
    // SoftDelete
    // ---------------------------------------------------------------------------

    [Fact]
    public void SoftDelete_SetsFlagsAndRecordsTimestamp()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Workspace");
        var before = DateTime.UtcNow;

        tenancy.SoftDelete();

        Assert.True(tenancy.IsSoftDeleted);
        Assert.NotNull(tenancy.SoftDeletedAt);
        Assert.True(tenancy.SoftDeletedAt >= before);
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_DoesNotChangeSoftDeletedAt()
    {
        var tenancy = Tenancy.Create(ValidOwnerAccountId, "Workspace");
        tenancy.SoftDelete();
        var firstDeletion = tenancy.SoftDeletedAt;

        tenancy.SoftDelete();

        Assert.Equal(firstDeletion, tenancy.SoftDeletedAt);
    }
}
