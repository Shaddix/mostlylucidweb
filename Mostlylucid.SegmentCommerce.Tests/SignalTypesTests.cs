using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class SignalTypesTests
{
    [Theory]
    [InlineData(SignalTypes.PageView, 0.01)]
    [InlineData(SignalTypes.ProductView, 0.10)]
    [InlineData(SignalTypes.AddToCart, 0.35)]
    [InlineData(SignalTypes.Purchase, 1.00)]
    [InlineData(SignalTypes.RemoveFromCart, -0.10)]
    public void GetBaseWeight_ReturnsCorrectWeight(string signalType, double expectedWeight)
    {
        var weight = SignalTypes.GetBaseWeight(signalType);
        Assert.Equal(expectedWeight, weight, precision: 2);
    }

    [Fact]
    public void GetBaseWeight_ReturnsDefault_ForUnknownType()
    {
        var weight = SignalTypes.GetBaseWeight("unknown-signal-type");
        Assert.Equal(0.05, weight, precision: 2);
    }

    [Fact]
    public void BaseWeights_AreOrdered_ByIntentLevel()
    {
        // Passive < Active < High-intent < Conversion
        Assert.True(SignalTypes.BaseWeights[SignalTypes.PageView] < SignalTypes.BaseWeights[SignalTypes.ProductView]);
        Assert.True(SignalTypes.BaseWeights[SignalTypes.ProductView] < SignalTypes.BaseWeights[SignalTypes.AddToCart]);
        Assert.True(SignalTypes.BaseWeights[SignalTypes.AddToCart] < SignalTypes.BaseWeights[SignalTypes.Purchase]);
    }

    [Fact]
    public void RemoveFromCart_HasNegativeWeight()
    {
        Assert.True(SignalTypes.BaseWeights[SignalTypes.RemoveFromCart] < 0);
    }
}

public class ElevatableSignalsTests
{
    [Theory]
    [InlineData(SignalTypes.Purchase, 0.0, true)]
    [InlineData(SignalTypes.AddToCart, 0.0, true)]
    [InlineData(SignalTypes.AddToWishlist, 0.0, true)]
    [InlineData(SignalTypes.Review, 0.0, true)]
    public void ShouldElevate_ReturnsTrue_ForAlwaysElevateSignals(string signalType, double weight, bool expected)
    {
        var result = ElevatableSignals.ShouldElevate(signalType, weight);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(SignalTypes.ProductView, 0.20, true)]
    [InlineData(SignalTypes.ProductView, 0.14, false)]
    [InlineData(SignalTypes.PageView, 0.15, true)]
    [InlineData(SignalTypes.PageView, 0.10, false)]
    public void ShouldElevate_RespectsThreshold_ForOtherSignals(string signalType, double weight, bool expected)
    {
        var result = ElevatableSignals.ShouldElevate(signalType, weight);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ElevationThreshold_Is015()
    {
        Assert.Equal(0.15, ElevatableSignals.ElevationThreshold);
    }
}

public class ProfileSegmentsTests
{
    [Fact]
    public void ProfileSegments_CanCombineFlags()
    {
        var segments = ProfileSegments.TechEnthusiast | ProfileSegments.HighEngagement | ProfileSegments.DesktopUser;

        Assert.True(segments.HasFlag(ProfileSegments.TechEnthusiast));
        Assert.True(segments.HasFlag(ProfileSegments.HighEngagement));
        Assert.True(segments.HasFlag(ProfileSegments.DesktopUser));
        Assert.False(segments.HasFlag(ProfileSegments.MobileUser));
    }

    [Fact]
    public void ProfileSegments_None_HasNoFlags()
    {
        var segments = ProfileSegments.None;

        Assert.False(segments.HasFlag(ProfileSegments.TechEnthusiast));
        Assert.False(segments.HasFlag(ProfileSegments.NewVisitor));
    }

    [Fact]
    public void ProfileSegments_CanAddAndRemoveFlags()
    {
        var segments = ProfileSegments.NewVisitor | ProfileSegments.BrowseOnly;
        
        // Add flag
        segments |= ProfileSegments.TechEnthusiast;
        Assert.True(segments.HasFlag(ProfileSegments.TechEnthusiast));

        // Remove flag
        segments &= ~ProfileSegments.BrowseOnly;
        Assert.False(segments.HasFlag(ProfileSegments.BrowseOnly));
        Assert.True(segments.HasFlag(ProfileSegments.NewVisitor));
    }

    [Fact]
    public void ProfileIdentificationMode_HasCorrectValues()
    {
        Assert.Equal(0, (int)ProfileIdentificationMode.None);
        Assert.Equal(1, (int)ProfileIdentificationMode.Fingerprint);
        Assert.Equal(2, (int)ProfileIdentificationMode.Cookie);
        Assert.Equal(3, (int)ProfileIdentificationMode.Identity);
    }
}
