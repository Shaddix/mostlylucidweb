using Microsoft.AspNetCore.Http;
using Moq;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Services.Segments;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class DemoUserServiceTests
{
    private static Mock<ISegmentService> CreateMockSegmentService()
    {
        var mock = new Mock<ISegmentService>();
        
        mock.Setup(x => x.GetSegments()).Returns(new List<SegmentDefinition>
        {
            new() { Id = "tech-enthusiast", Name = "Tech Enthusiasts", Icon = "🔧", Color = "#3b82f6", MembershipThreshold = 0.35 },
            new() { Id = "high-value", Name = "High-Value Customers", Icon = "💎", Color = "#8b5cf6", MembershipThreshold = 0.4 }
        });

        mock.Setup(x => x.GetSegment("tech-enthusiast")).Returns(
            new SegmentDefinition { Id = "tech-enthusiast", Name = "Tech Enthusiasts", Icon = "🔧", Color = "#3b82f6" });
        
        mock.Setup(x => x.GetSegment("non-existing")).Returns((SegmentDefinition?)null);
        
        mock.Setup(x => x.ComputeMemberships(It.IsAny<ProfileData>()))
            .Returns(new List<SegmentMembership>
            {
                new() { SegmentId = "tech-enthusiast", SegmentName = "Tech Enthusiasts", Score = 0.8, IsMember = true, SegmentIcon = "🔧", SegmentColor = "#3b82f6" }
            });

        return mock;
    }

    private static (DemoUserService Sut, SegmentCommerceDbContext Db, Mock<ISegmentService> Mock) CreateSut()
    {
        var db = TestDbContextBase.Create();
        var mock = CreateMockSegmentService();
        var sut = new DemoUserService(db, mock.Object);
        return (sut, db, mock);
    }

    #region GetDemoUsersAsync Tests

    [Fact]
    public async Task GetDemoUsersAsync_NoProfiles_ReturnsEmptyList()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;

        // Act
        var result = await sut.GetDemoUsersAsync(10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDemoUsersAsync_WithProfiles_ReturnsDemoUsers()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        await SeedTestProfiles(db);

        // Act
        var result = await sut.GetDemoUsersAsync(5);

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Count <= 5);
    }

    [Fact]
    public async Task GetDemoUsersAsync_ReturnsProfilesWithSegmentInfo()
    {
        // Arrange
        var (sut, db, mock) = CreateSut();
        using var _ = db;
        await SeedTestProfiles(db);
        
        mock.Setup(x => x.ComputeMemberships(It.IsAny<ProfileData>()))
            .Returns(new List<SegmentMembership>
            {
                new() { SegmentId = "tech-enthusiast", SegmentName = "Tech Enthusiasts", Score = 0.8, IsMember = true, SegmentIcon = "🔧", SegmentColor = "#3b82f6" },
                new() { SegmentId = "high-value", SegmentName = "High-Value", Score = 0.5, IsMember = true, SegmentIcon = "💎", SegmentColor = "#8b5cf6" }
            });

        // Act
        var result = await sut.GetDemoUsersAsync(5);

        // Assert
        Assert.NotEmpty(result);
        var user = result.First();
        Assert.NotNull(user.PrimarySegment);
        Assert.Equal("tech-enthusiast", user.PrimarySegment.Id);
        Assert.Equal(2, user.SegmentCount);
    }

    #endregion

    #region GetDemoUsersBySegmentAsync Tests

    [Fact]
    public async Task GetDemoUsersBySegmentAsync_NonExistingSegment_ReturnsEmptyList()
    {
        // Arrange
        var (sut, db, mock) = CreateSut();
        using var _ = db;
        mock.Setup(x => x.GetSegment("non-existing")).Returns((SegmentDefinition?)null);

        // Act
        var result = await sut.GetDemoUsersBySegmentAsync("non-existing", 5);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDemoUsersBySegmentAsync_ExistingSegment_ReturnsMatchingUsers()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        await SeedTestProfiles(db);

        // Act
        var result = await sut.GetDemoUsersBySegmentAsync("tech-enthusiast", 5);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, u => Assert.Equal("tech-enthusiast", u.PrimarySegment?.Id));
    }

    #endregion

    #region LoginAsDemoUserAsync Tests

    [Fact]
    public async Task LoginAsDemoUserAsync_NonExistingProfile_ReturnsFailure()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var mockSession = CreateMockSession();
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = await sut.LoginAsDemoUserAsync(Guid.NewGuid(), mockHttpContext.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Profile not found", result.Error);
    }

    [Fact]
    public async Task LoginAsDemoUserAsync_ExistingProfile_SetsSessionAndReturnsSuccess()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var profileId = Guid.NewGuid();
        await SeedTestProfiles(db, profileId);
        
        var sessionData = new Dictionary<string, byte[]>();
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = await sut.LoginAsDemoUserAsync(profileId, mockHttpContext.Object);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Profile);
        Assert.Equal(profileId, result.Profile.Id);
        Assert.NotEmpty(result.Segments!);
        
        // Verify session was set
        Assert.True(sessionData.ContainsKey("DemoUserProfileId"));
    }

    #endregion

    #region LogoutDemoUser Tests

    [Fact]
    public void LogoutDemoUser_RemovesSessionKey()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var sessionData = new Dictionary<string, byte[]>
        {
            ["DemoUserProfileId"] = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())
        };
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        sut.LogoutDemoUser(mockHttpContext.Object);

        // Assert
        Assert.False(sessionData.ContainsKey("DemoUserProfileId"));
    }

    #endregion

    #region GetCurrentDemoUserAsync Tests

    [Fact]
    public async Task GetCurrentDemoUserAsync_NoSession_ReturnsNull()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var mockSession = CreateMockSession();
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = await sut.GetCurrentDemoUserAsync(mockHttpContext.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentDemoUserAsync_InvalidGuidInSession_ReturnsNull()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var sessionData = new Dictionary<string, byte[]>
        {
            ["DemoUserProfileId"] = System.Text.Encoding.UTF8.GetBytes("not-a-guid")
        };
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = await sut.GetCurrentDemoUserAsync(mockHttpContext.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentDemoUserAsync_ValidSession_DeletedProfile_ClearsSessionAndReturnsNull()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var deletedProfileId = Guid.NewGuid(); // Not in DB
        var sessionData = new Dictionary<string, byte[]>
        {
            ["DemoUserProfileId"] = System.Text.Encoding.UTF8.GetBytes(deletedProfileId.ToString())
        };
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = await sut.GetCurrentDemoUserAsync(mockHttpContext.Object);

        // Assert
        Assert.Null(result);
        Assert.False(sessionData.ContainsKey("DemoUserProfileId"));
    }

    [Fact]
    public async Task GetCurrentDemoUserAsync_ValidSession_ExistingProfile_ReturnsContext()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var profileId = Guid.NewGuid();
        await SeedTestProfiles(db, profileId);

        var sessionData = new Dictionary<string, byte[]>
        {
            ["DemoUserProfileId"] = System.Text.Encoding.UTF8.GetBytes(profileId.ToString())
        };
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = await sut.GetCurrentDemoUserAsync(mockHttpContext.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(profileId, result.Profile.Id);
        Assert.NotEmpty(result.Segments);
    }

    #endregion

    #region IsDemoMode Tests

    [Fact]
    public void IsDemoMode_NoSession_ReturnsFalse()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var mockSession = CreateMockSession();
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = sut.IsDemoMode(mockHttpContext.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDemoMode_WithSession_ReturnsTrue()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var sessionData = new Dictionary<string, byte[]>
        {
            ["DemoUserProfileId"] = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())
        };
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = sut.IsDemoMode(mockHttpContext.Object);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region GetCurrentDemoProfileId Tests

    [Fact]
    public void GetCurrentDemoProfileId_NoSession_ReturnsNull()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var mockSession = CreateMockSession();
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = sut.GetCurrentDemoProfileId(mockHttpContext.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCurrentDemoProfileId_WithValidSession_ReturnsProfileId()
    {
        // Arrange
        var (sut, db, _) = CreateSut();
        using var _ = db;
        var profileId = Guid.NewGuid();
        var sessionData = new Dictionary<string, byte[]>
        {
            ["DemoUserProfileId"] = System.Text.Encoding.UTF8.GetBytes(profileId.ToString())
        };
        var mockSession = CreateMockSession(sessionData);
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.Session).Returns(mockSession.Object);

        // Act
        var result = sut.GetCurrentDemoProfileId(mockHttpContext.Object);

        // Assert
        Assert.Equal(profileId, result);
    }

    #endregion

    #region Helper Methods

    private static async Task SeedTestProfiles(SegmentCommerceDbContext db, Guid? specificId = null)
    {
        var profiles = new List<PersistentProfileEntity>
        {
            new()
            {
                Id = specificId ?? Guid.NewGuid(),
                ProfileKey = "test-profile-1",
                IdentificationMode = ProfileIdentificationMode.Fingerprint,
                TotalSessions = 5,
                TotalSignals = 20,
                TotalPurchases = 2,
                TotalCartAdds = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastSeenAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProfileKey = "test-profile-2",
                IdentificationMode = ProfileIdentificationMode.Cookie,
                TotalSessions = 10,
                TotalSignals = 50,
                TotalPurchases = 8,
                TotalCartAdds = 15,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                LastSeenAt = DateTime.UtcNow
            }
        };

        db.PersistentProfiles.AddRange(profiles);
        await db.SaveChangesAsync();
    }

    private static Mock<ISession> CreateMockSession(Dictionary<string, byte[]>? data = null)
    {
        data ??= new Dictionary<string, byte[]>();
        var mockSession = new Mock<ISession>();

        mockSession.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]?>.IsAny))
            .Returns((string key, out byte[]? value) =>
            {
                var exists = data.TryGetValue(key, out var val);
                value = val;
                return exists;
            });

        mockSession.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback((string key, byte[] value) => data[key] = value);

        mockSession.Setup(s => s.Remove(It.IsAny<string>()))
            .Callback((string key) => data.Remove(key));

        return mockSession;
    }

    #endregion
}
