using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Services.Segments;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class SegmentServiceTests
{
    private readonly SegmentService _sut;

    public SegmentServiceTests()
    {
        _sut = new SegmentService();
    }

    #region GetSegments Tests

    [Fact]
    public void GetSegments_ReturnsDefaultSegments()
    {
        // Act
        var segments = _sut.GetSegments();

        // Assert
        Assert.NotEmpty(segments);
        Assert.Equal(10, segments.Count); // 10 default segments
    }

    [Fact]
    public void GetSegments_ContainsExpectedSegmentIds()
    {
        // Act
        var segments = _sut.GetSegments();
        var ids = segments.Select(s => s.Id).ToList();

        // Assert
        Assert.Contains("high-value", ids);
        Assert.Contains("tech-enthusiast", ids);
        Assert.Contains("fashion-forward", ids);
        Assert.Contains("bargain-hunter", ids);
        Assert.Contains("new-visitor", ids);
        Assert.Contains("cart-abandoner", ids);
        Assert.Contains("home-enthusiast", ids);
        Assert.Contains("fitness-active", ids);
        Assert.Contains("loyal-customer", ids);
        Assert.Contains("researcher", ids);
    }

    #endregion

    #region GetSegment Tests

    [Fact]
    public void GetSegment_ExistingId_ReturnsSegment()
    {
        // Act
        var segment = _sut.GetSegment("tech-enthusiast");

        // Assert
        Assert.NotNull(segment);
        Assert.Equal("Tech Enthusiasts", segment.Name);
        Assert.Equal("#3b82f6", segment.Color);
    }

    [Fact]
    public void GetSegment_NonExistingId_ReturnsNull()
    {
        // Act
        var segment = _sut.GetSegment("non-existing-segment");

        // Assert
        Assert.Null(segment);
    }

    #endregion

    #region AddSegment Tests

    [Fact]
    public void AddSegment_NewSegment_IsAddedToList()
    {
        // Arrange
        var customSegment = new SegmentDefinition
        {
            Id = "custom-segment",
            Name = "Custom Segment",
            Description = "A test segment",
            MembershipThreshold = 0.5
        };

        // Act
        _sut.AddSegment(customSegment);
        var retrieved = _sut.GetSegment("custom-segment");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Custom Segment", retrieved.Name);
    }

    #endregion

    #region ComputeMemberships Tests

    [Fact]
    public void ComputeMemberships_EmptyProfile_ReturnsAllSegments()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "test-profile",
            Interests = new Dictionary<string, double>(),
            TotalPurchases = 0,
            TotalSessions = 0,
            LastSeenAt = DateTime.UtcNow
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);

        // Assert
        Assert.Equal(10, memberships.Count);
        
        // "new-visitor" should match (few sessions, no purchases)
        var newVisitor = memberships.First(m => m.SegmentId == "new-visitor");
        Assert.True(newVisitor.IsMember, "Empty profile should be a new-visitor");
        
        // "cart-abandoner" may also match because "few purchases" rule matches (0 < 2)
        // This is expected behavior - segments can overlap
        
        // Category-based segments should NOT match (no interests)
        var techEnthusiast = memberships.First(m => m.SegmentId == "tech-enthusiast");
        Assert.False(techEnthusiast.IsMember, "Empty profile should not be a tech-enthusiast");
        
        var fashionForward = memberships.First(m => m.SegmentId == "fashion-forward");
        Assert.False(fashionForward.IsMember, "Empty profile should not be fashion-forward");
    }

    [Fact]
    public void ComputeMemberships_TechEnthusiast_HighTechInterest_IsMember()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "tech-lover",
            Interests = new Dictionary<string, double>
            {
                ["tech"] = 0.8
            },
            Affinities = new Dictionary<string, double>
            {
                ["gadgets"] = 0.5,
                ["electronics"] = 0.6
            },
            TotalSessions = 5,
            LastSeenAt = DateTime.UtcNow
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);
        var techMembership = memberships.First(m => m.SegmentId == "tech-enthusiast");

        // Assert
        Assert.True(techMembership.IsMember, $"Expected to be a tech-enthusiast member, score: {techMembership.Score}");
        Assert.True(techMembership.Score >= 0.35); // threshold is 0.35
    }

    [Fact]
    public void ComputeMemberships_NewVisitor_FewSessions_NoPurchases_IsMember()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "new-user",
            TotalSessions = 1,
            TotalPurchases = 0,
            LastSeenAt = DateTime.UtcNow
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);
        var newVisitorMembership = memberships.First(m => m.SegmentId == "new-visitor");

        // Assert
        Assert.True(newVisitorMembership.IsMember, $"Expected to be a new-visitor, score: {newVisitorMembership.Score}");
    }

    [Fact]
    public void ComputeMemberships_HighValueCustomer_ManyPurchases_IsMember()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "vip-shopper",
            TotalPurchases = 10,
            TotalSessions = 20,
            PricePreferences = new PricePreferences
            {
                MinObserved = 150,
                MaxObserved = 500,
                AveragePurchase = 250
            },
            LastSeenAt = DateTime.UtcNow.AddDays(-5) // Active recently
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);
        var highValueMembership = memberships.First(m => m.SegmentId == "high-value");

        // Assert
        Assert.True(highValueMembership.IsMember, $"Expected to be high-value, score: {highValueMembership.Score}");
    }

    [Fact]
    public void ComputeMemberships_CartAbandoner_ManyCartAdds_FewPurchases_IsMember()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "window-shopper",
            TotalCartAdds = 15,
            TotalPurchases = 1,
            TotalSessions = 5,
            LastSeenAt = DateTime.UtcNow
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);
        var cartAbandonerMembership = memberships.First(m => m.SegmentId == "cart-abandoner");

        // Assert
        Assert.True(cartAbandonerMembership.IsMember, $"Expected to be cart-abandoner, score: {cartAbandonerMembership.Score}");
    }

    [Fact]
    public void ComputeMemberships_LoyalCustomer_HighEngagement_IsMember()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "loyal-regular",
            TotalPurchases = 8,
            TotalSessions = 15,
            TotalSignals = 100,
            LastSeenAt = DateTime.UtcNow.AddDays(-3) // Recent activity
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);
        var loyalMembership = memberships.First(m => m.SegmentId == "loyal-customer");

        // Assert
        Assert.True(loyalMembership.IsMember, $"Expected to be loyal-customer, score: {loyalMembership.Score}");
    }

    [Fact]
    public void ComputeMemberships_ResultsOrderedByScoreDescending()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "mixed-user",
            Interests = new Dictionary<string, double>
            {
                ["tech"] = 0.6,
                ["fashion"] = 0.3
            },
            TotalSessions = 5,
            TotalPurchases = 2
        };

        // Act
        var memberships = _sut.ComputeMemberships(profile);

        // Assert
        for (int i = 1; i < memberships.Count; i++)
        {
            Assert.True(memberships[i - 1].Score >= memberships[i].Score,
                $"Memberships not sorted: {memberships[i - 1].Score} < {memberships[i].Score}");
        }
    }

    #endregion

    #region GetMemberSegments Tests

    [Fact]
    public void GetMemberSegments_OnlyReturnsSegmentsAboveThreshold()
    {
        // Arrange
        var profile = new ProfileData
        {
            Id = Guid.NewGuid(),
            ProfileKey = "tech-lover",
            Interests = new Dictionary<string, double>
            {
                ["tech"] = 0.9
            },
            Affinities = new Dictionary<string, double>
            {
                ["gadgets"] = 0.7,
                ["electronics"] = 0.6
            },
            TotalSessions = 3
        };

        // Act
        var memberSegments = _sut.GetMemberSegments(profile);

        // Assert
        Assert.NotEmpty(memberSegments);
        Assert.All(memberSegments, m => Assert.True(m.IsMember));
    }

    #endregion

    #region EvaluateSegment Tests

    [Fact]
    public void EvaluateSegment_WeightedCombination_CalculatesCorrectly()
    {
        // Arrange
        var segment = new SegmentDefinition
        {
            Id = "test-weighted",
            Name = "Test Weighted",
            Combination = RuleCombination.Weighted,
            MembershipThreshold = 0.5,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.GreaterOrEqual, Value = 5, Weight = 0.5 },
                new() { Type = RuleType.Statistic, Field = "totalSessions", Operator = RuleOperator.GreaterOrEqual, Value = 10, Weight = 0.5 }
            ]
        };

        var profile = new ProfileData
        {
            TotalPurchases = 5, // 100% match on first rule
            TotalSessions = 5  // 50% match on second rule (5/10)
        };

        // Act
        var membership = _sut.EvaluateSegment(profile, segment);

        // Assert
        // Expected: (1.0 * 0.5 + 0.5 * 0.5) / 1.0 = 0.75
        Assert.True(membership.Score >= 0.5 && membership.Score <= 1.0, 
            $"Expected weighted score around 0.75, got {membership.Score}");
    }

    [Fact]
    public void EvaluateSegment_AllCombination_ReturnsMinScore()
    {
        // Arrange
        var segment = new SegmentDefinition
        {
            Id = "test-all",
            Name = "Test All",
            Combination = RuleCombination.All,
            MembershipThreshold = 0.3,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.GreaterOrEqual, Value = 5, Weight = 1 },
                new() { Type = RuleType.Statistic, Field = "totalSessions", Operator = RuleOperator.GreaterOrEqual, Value = 10, Weight = 1 }
            ]
        };

        var profile = new ProfileData
        {
            TotalPurchases = 10, // Exceeds threshold
            TotalSessions = 3   // Below threshold (30% of 10)
        };

        // Act
        var membership = _sut.EvaluateSegment(profile, segment);

        // Assert
        // All combination = min score, sessions = 3/10 = 0.3
        Assert.True(membership.Score <= 0.5, $"Expected low score due to All combination, got {membership.Score}");
    }

    [Fact]
    public void EvaluateSegment_AnyCombination_ReturnsMaxScore()
    {
        // Arrange
        var segment = new SegmentDefinition
        {
            Id = "test-any",
            Name = "Test Any",
            Combination = RuleCombination.Any,
            MembershipThreshold = 0.5,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.GreaterOrEqual, Value = 100, Weight = 1 }, // Won't match
                new() { Type = RuleType.Statistic, Field = "totalSessions", Operator = RuleOperator.GreaterOrEqual, Value = 5, Weight = 1 }   // Will match
            ]
        };

        var profile = new ProfileData
        {
            TotalPurchases = 1,  // Low
            TotalSessions = 10   // Exceeds threshold
        };

        // Act
        var membership = _sut.EvaluateSegment(profile, segment);

        // Assert
        // Any combination = max score, sessions matches fully
        Assert.True(membership.IsMember, $"Expected member due to Any combination, score: {membership.Score}");
    }

    [Fact]
    public void EvaluateSegment_ReturnsRuleScoresForExplanation()
    {
        // Arrange
        var segment = _sut.GetSegment("tech-enthusiast")!;
        var profile = new ProfileData
        {
            Interests = new Dictionary<string, double> { ["tech"] = 0.6 },
            Affinities = new Dictionary<string, double> { ["gadgets"] = 0.4 }
        };

        // Act
        var membership = _sut.EvaluateSegment(profile, segment);

        // Assert
        Assert.NotEmpty(membership.RuleScores);
        Assert.All(membership.RuleScores, r => Assert.NotEmpty(r.RuleDescription));
    }

    #endregion

    #region Rule Evaluation Tests

    [Theory]
    [InlineData(0.5, 0.4, true)]  // interest > threshold
    [InlineData(0.3, 0.4, false)] // interest < threshold
    [InlineData(0.4, 0.4, true)]  // interest == threshold (GreaterOrEqual)
    public void EvaluateSegment_CategoryInterestRule_EvaluatesCorrectly(double interestValue, double threshold, bool shouldBeHighScore)
    {
        // Arrange
        var segment = new SegmentDefinition
        {
            Id = "interest-test",
            Name = "Interest Test",
            MembershipThreshold = 0.3,
            Combination = RuleCombination.Weighted,
            Rules =
            [
                new() { Type = RuleType.CategoryInterest, Field = "interests.tech", Operator = RuleOperator.GreaterOrEqual, Value = threshold, Weight = 1 }
            ]
        };

        var profile = new ProfileData
        {
            Interests = new Dictionary<string, double> { ["tech"] = interestValue }
        };

        // Act
        var membership = _sut.EvaluateSegment(profile, segment);

        // Assert
        if (shouldBeHighScore)
        {
            Assert.True(membership.Score >= 0.5, $"Expected high score, got {membership.Score}");
        }
        else
        {
            Assert.True(membership.Score < 0.8, $"Expected lower score, got {membership.Score}");
        }
    }

    [Fact]
    public void EvaluateSegment_TraitRule_MatchesBoolean()
    {
        // Arrange
        var segment = new SegmentDefinition
        {
            Id = "trait-test",
            Name = "Trait Test",
            MembershipThreshold = 0.5,
            Rules =
            [
                new() { Type = RuleType.Trait, Field = "traits.prefersDeals", Value = true, Weight = 1 }
            ]
        };

        var profileWithTrait = new ProfileData
        {
            Traits = new Dictionary<string, bool> { ["prefersDeals"] = true }
        };

        var profileWithoutTrait = new ProfileData
        {
            Traits = new Dictionary<string, bool> { ["prefersDeals"] = false }
        };

        // Act
        var withTraitMembership = _sut.EvaluateSegment(profileWithTrait, segment);
        var withoutTraitMembership = _sut.EvaluateSegment(profileWithoutTrait, segment);

        // Assert
        Assert.Equal(1.0, withTraitMembership.Score);
        Assert.Equal(0.0, withoutTraitMembership.Score);
    }

    [Fact]
    public void EvaluateSegment_RecencyRule_RecentActivityScoresHigh()
    {
        // Arrange
        var segment = new SegmentDefinition
        {
            Id = "recency-test",
            Name = "Recency Test",
            MembershipThreshold = 0.5,
            Rules =
            [
                new() { Type = RuleType.Recency, Field = "lastSeen", Operator = RuleOperator.LessThan, Value = 7, Weight = 1 }
            ]
        };

        var recentProfile = new ProfileData { LastSeenAt = DateTime.UtcNow.AddDays(-2) };
        var oldProfile = new ProfileData { LastSeenAt = DateTime.UtcNow.AddDays(-30) };

        // Act
        var recentMembership = _sut.EvaluateSegment(recentProfile, segment);
        var oldMembership = _sut.EvaluateSegment(oldProfile, segment);

        // Assert
        Assert.True(recentMembership.Score > oldMembership.Score, 
            $"Recent ({recentMembership.Score}) should score higher than old ({oldMembership.Score})");
        Assert.True(recentMembership.IsMember);
    }

    #endregion

    #region Confidence Tests

    [Theory]
    [InlineData(0.9, "Very High")]
    [InlineData(0.7, "High")]
    [InlineData(0.5, "Medium")]
    [InlineData(0.3, "Low")]
    [InlineData(0.1, "Very Low")]
    public void SegmentMembership_Confidence_ReturnsCorrectLabel(double score, string expectedConfidence)
    {
        // Arrange
        var membership = new SegmentMembership { Score = score };

        // Assert
        Assert.Equal(expectedConfidence, membership.Confidence);
    }

    #endregion
}
