using DraftView.Application.Services;

namespace DraftView.Application.Tests.Services;

/// <summary>
/// Tests for PassageAnchorConfidence scoring and threshold rules.
/// Covers: normalization, exact and context confidence constants, and fuzzy acceptance thresholds.
/// Excludes: anchor resolution, authorization, and persistence.
/// </summary>
public class PassageAnchorConfidenceTests
{
    [Fact]
    public void FromEditDistance_ExactMatch_Returns100()
    {
        Assert.Equal(100, PassageAnchorConfidence.FromEditDistance("Alpha beta", "Alpha beta"));
    }

    [Fact]
    public void FromEditDistance_MinorVariation_ReturnsDeterministicScore()
    {
        Assert.Equal(80, PassageAnchorConfidence.FromEditDistance("Alpha beta", "Alfa beta"));
    }

    [Fact]
    public void IsFuzzyMatchAcceptable_UsesThreshold()
    {
        Assert.False(PassageAnchorConfidence.IsFuzzyMatchAcceptable(64));
        Assert.True(PassageAnchorConfidence.IsFuzzyMatchAcceptable(65));
    }
}
