using DraftView.Domain.Entities;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Exceptions;

namespace DraftView.Domain.Tests.Entities;

/// <summary>
/// Tests for the TenancySubscription entity.
/// Covers: factory method invariants, tier updates, provider id storage, deactivation.
/// Excludes: billing provider integration, enforcement rules (MT-Sprint-2 rollout phase).
/// </summary>
public class TenancySubscriptionTests
{
    private static readonly Guid ValidTenancyId = Guid.NewGuid();

    // ---------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_ReturnsSubscription()
    {
        var subscription = TenancySubscription.Create(ValidTenancyId, SubscriptionTier.Free);

        Assert.NotEqual(Guid.Empty, subscription.Id);
        Assert.Equal(ValidTenancyId, subscription.TenancyId);
        Assert.Equal(SubscriptionTier.Free, subscription.Tier);
        Assert.True(subscription.IsActive);
        Assert.Null(subscription.ProviderSubscriptionId);
        Assert.Null(subscription.UpdatedAt);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var subscription = TenancySubscription.Create(ValidTenancyId, SubscriptionTier.Free);

        Assert.True(subscription.CreatedAt >= before);
        Assert.True(subscription.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyTenancyId_ThrowsInvariantViolationException()
    {
        var ex = Assert.Throws<InvariantViolationException>(
            () => TenancySubscription.Create(Guid.Empty, SubscriptionTier.Free));

        Assert.Equal("I-TSUB-TENANCY", ex.InvariantCode);
    }

    [Theory]
    [InlineData(SubscriptionTier.Free)]
    [InlineData(SubscriptionTier.Paid)]
    [InlineData(SubscriptionTier.Ultimate)]
    public void Create_WithAnyTier_SetsTierCorrectly(SubscriptionTier tier)
    {
        var subscription = TenancySubscription.Create(ValidTenancyId, tier);

        Assert.Equal(tier, subscription.Tier);
    }

    // ---------------------------------------------------------------------------
    // UpdateTier
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateTier_ChangesTierAndSetsUpdatedAt()
    {
        var subscription = TenancySubscription.Create(ValidTenancyId, SubscriptionTier.Free);
        var before = DateTime.UtcNow;

        subscription.UpdateTier(SubscriptionTier.Paid);

        Assert.Equal(SubscriptionTier.Paid, subscription.Tier);
        Assert.NotNull(subscription.UpdatedAt);
        Assert.True(subscription.UpdatedAt >= before);
    }

    // ---------------------------------------------------------------------------
    // SetProviderSubscriptionId
    // ---------------------------------------------------------------------------

    [Fact]
    public void SetProviderSubscriptionId_StoresIdAndSetsUpdatedAt()
    {
        var subscription = TenancySubscription.Create(ValidTenancyId, SubscriptionTier.Free);
        var before = DateTime.UtcNow;

        subscription.SetProviderSubscriptionId("prov_abc123");

        Assert.Equal("prov_abc123", subscription.ProviderSubscriptionId);
        Assert.NotNull(subscription.UpdatedAt);
        Assert.True(subscription.UpdatedAt >= before);
    }

    // ---------------------------------------------------------------------------
    // Deactivate
    // ---------------------------------------------------------------------------

    [Fact]
    public void Deactivate_SetsIsActiveFalseAndSetsUpdatedAt()
    {
        var subscription = TenancySubscription.Create(ValidTenancyId, SubscriptionTier.Paid);
        var before = DateTime.UtcNow;

        subscription.Deactivate();

        Assert.False(subscription.IsActive);
        Assert.NotNull(subscription.UpdatedAt);
        Assert.True(subscription.UpdatedAt >= before);
    }
}
