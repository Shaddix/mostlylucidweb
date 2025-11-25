using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mostlylucid.Announcement.Manager.Services;
using Mostlylucid.Shared.Models;
using RichardSzalay.MockHttp;

namespace Mostlylucid.Announcement.Manager.Test;

public class AnnouncementApiClientTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task GetAllAnnouncementsAsync_ReturnsAnnouncements()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var announcements = new List<AnnouncementDto>
        {
            new() { Id = 1, Key = "test-1", Markdown = "Test 1", Language = "en", IsActive = true },
            new() { Id = 2, Key = "test-2", Markdown = "Test 2", Language = "en", IsActive = false }
        };

        mockHttp.When("https://test.local/api/announcement/all")
            .Respond("application/json", JsonSerializer.Serialize(announcements, _jsonOptions));

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.GetAllAnnouncementsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("test-1", result[0].Key);
        Assert.Equal("test-2", result[1].Key);
    }

    [Fact]
    public async Task GetActiveAnnouncementAsync_ReturnsAnnouncement_WhenActive()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var announcement = new AnnouncementDto
        {
            Id = 1,
            Key = "active-announcement",
            Markdown = "Active content",
            HtmlContent = "<p>Active content</p>",
            Language = "en",
            IsActive = true
        };

        mockHttp.When("https://test.local/api/announcement/active?language=en")
            .Respond("application/json", JsonSerializer.Serialize(announcement, _jsonOptions));

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local"
        };

        // Act
        var result = await client.GetActiveAnnouncementAsync("en");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("active-announcement", result.Key);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetActiveAnnouncementAsync_ReturnsNull_WhenNoActive()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("https://test.local/api/announcement/active?language=en")
            .Respond("application/json", "null");

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local"
        };

        // Act
        var result = await client.GetActiveAnnouncementAsync("en");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAnnouncementAsync_ReturnsAnnouncement_WhenExists()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var announcement = new AnnouncementDto
        {
            Id = 1,
            Key = "specific-announcement",
            Markdown = "Specific content",
            Language = "en"
        };

        mockHttp.When("https://test.local/api/announcement/specific-announcement?language=en")
            .Respond("application/json", JsonSerializer.Serialize(announcement, _jsonOptions));

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.GetAnnouncementAsync("specific-announcement", "en");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("specific-announcement", result.Key);
    }

    [Fact]
    public async Task GetAnnouncementAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("https://test.local/api/announcement/nonexistent?language=en")
            .Respond(HttpStatusCode.NotFound);

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.GetAnnouncementAsync("nonexistent", "en");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertAnnouncementAsync_CreatesAnnouncement()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var request = new CreateAnnouncementRequest
        {
            Key = "new-announcement",
            Markdown = "New content",
            Language = "en",
            IsActive = true,
            Priority = 10
        };

        var response = new AnnouncementDto
        {
            Id = 1,
            Key = "new-announcement",
            Markdown = "New content",
            HtmlContent = "<p>New content</p>",
            Language = "en",
            IsActive = true,
            Priority = 10
        };

        mockHttp.When(HttpMethod.Post, "https://test.local/api/announcement")
            .Respond("application/json", JsonSerializer.Serialize(response, _jsonOptions));

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.UpsertAnnouncementAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("new-announcement", result.Key);
        Assert.Equal("<p>New content</p>", result.HtmlContent);
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Delete, "https://test.local/api/announcement/to-delete?language=en")
            .Respond(HttpStatusCode.OK);

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.DeleteAnnouncementAsync("to-delete", "en");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Delete, "https://test.local/api/announcement/nonexistent?language=en")
            .Respond(HttpStatusCode.NotFound);

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.DeleteAnnouncementAsync("nonexistent", "en");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeactivateAnnouncementAsync_ReturnsTrue_WhenSuccess()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Post, "https://test.local/api/announcement/active-key/deactivate?language=en")
            .Respond(HttpStatusCode.OK);

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "test-token"
        };

        // Act
        var result = await client.DeactivateAnnouncementAsync("active-key", "en");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetAllAnnouncementsAsync_SetsAuthHeader()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("https://test.local/api/announcement/all")
            .WithHeaders("X-Api-Token", "secret-token")
            .Respond("application/json", "[]");

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local",
            ApiToken = "secret-token"
        };

        // Act
        var result = await client.GetAllAnnouncementsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveAnnouncementAsync_WorksWithDifferentLanguages()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var announcement = new AnnouncementDto
        {
            Id = 1,
            Key = "multilingual",
            Markdown = "Contenu français",
            Language = "fr"
        };

        mockHttp.When("https://test.local/api/announcement/active?language=fr")
            .Respond("application/json", JsonSerializer.Serialize(announcement, _jsonOptions));

        var client = new AnnouncementApiClient(mockHttp.ToHttpClient())
        {
            BaseUrl = "https://test.local"
        };

        // Act
        var result = await client.GetActiveAnnouncementAsync("fr");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fr", result.Language);
        Assert.Equal("Contenu français", result.Markdown);
    }
}
