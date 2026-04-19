using DraftView.Domain.Enumerations;
using DraftView.Domain.Policies;

namespace DraftView.Domain.Tests.Policies;

public class VersionRetentionPolicyTests
{
    [Fact]
    public void GetLimit_Free_ReturnsFreeLimit()
    {
        var limit = VersionRetentionPolicy.GetLimit(SubscriptionTier.Free);

        Assert.Equal(VersionRetentionPolicy.FreeLimit, limit);
    }

    [Fact]
    public void GetLimit_Paid_ReturnsPaidLimit()
    {
        var limit = VersionRetentionPolicy.GetLimit(SubscriptionTier.Paid);

        Assert.Equal(VersionRetentionPolicy.PaidLimit, limit);
    }

    [Fact]
    public void GetLimit_Ultimate_ReturnsUnlimited()
    {
        var limit = VersionRetentionPolicy.GetLimit(SubscriptionTier.Ultimate);

        Assert.Equal(VersionRetentionPolicy.Unlimited, limit);
    }

    [Fact]
    public void IsAtLimit_WhenBelowLimit_ReturnsFalse()
    {
        var atLimit = VersionRetentionPolicy.IsAtLimit(2, SubscriptionTier.Free);

        Assert.False(atLimit);
    }

    [Fact]
    public void IsAtLimit_WhenAtLimit_ReturnsTrue()
    {
        var atLimit = VersionRetentionPolicy.IsAtLimit(3, SubscriptionTier.Free);

        Assert.True(atLimit);
    }

    [Fact]
    public void IsAtLimit_WhenAboveLimit_ReturnsTrue()
    {
        var atLimit = VersionRetentionPolicy.IsAtLimit(11, SubscriptionTier.Paid);

        Assert.True(atLimit);
    }

    [Fact]
    public void IsAtLimit_Ultimate_NeverReturnsTrue()
    {
        var atLimit = VersionRetentionPolicy.IsAtLimit(int.MaxValue, SubscriptionTier.Ultimate);

        Assert.False(atLimit);
    }
}
