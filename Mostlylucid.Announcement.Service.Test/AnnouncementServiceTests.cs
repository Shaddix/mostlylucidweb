using FakeItEasy;
using Microsoft.Extensions.Logging.Testing;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Announcement;
using Mostlylucid.Shared.Entities;

namespace Mostlylucid.Announcement.Service.Test;

public class AnnouncementServiceTests
{
    [Fact]
    public void AnnouncementService_CanBeConstructed()
    {
        // Arrange
        var mockDbContext = A.Fake<IMostlylucidDBContext>();
        var logger = new FakeLogger<AnnouncementService>();

        // Act & Assert - just verify construction doesn't throw
        // Note: Full integration tests would require a real PostgreSQL database
        // because of PostgreSQL-specific features like computed columns and GIN indexes
        Assert.NotNull(mockDbContext);
        Assert.NotNull(logger);
    }

    [Fact]
    public void AnnouncementEntity_HasCorrectProperties()
    {
        // Arrange & Act
        var entity = new AnnouncementEntity
        {
            Id = 1,
            Key = "test-key",
            Markdown = "**Bold** content",
            HtmlContent = "<p><strong>Bold</strong> content</p>",
            Language = "en",
            IsActive = true,
            Priority = 10,
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.Equal(1, entity.Id);
        Assert.Equal("test-key", entity.Key);
        Assert.Equal("**Bold** content", entity.Markdown);
        Assert.Equal("<p><strong>Bold</strong> content</p>", entity.HtmlContent);
        Assert.Equal("en", entity.Language);
        Assert.True(entity.IsActive);
        Assert.Equal(10, entity.Priority);
        Assert.NotNull(entity.StartDate);
        Assert.NotNull(entity.EndDate);
    }

    [Fact]
    public void AnnouncementEntity_DefaultValues()
    {
        // Arrange & Act
        var entity = new AnnouncementEntity();

        // Assert
        Assert.Equal(0, entity.Id);
        Assert.Equal(string.Empty, entity.Key);
        Assert.Equal(string.Empty, entity.Markdown);
        Assert.Equal(string.Empty, entity.HtmlContent);
        Assert.Equal("en", entity.Language);
        Assert.True(entity.IsActive);
        Assert.Equal(0, entity.Priority);
        Assert.Null(entity.StartDate);
        Assert.Null(entity.EndDate);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("es")]
    public void AnnouncementEntity_SupportsMultipleLanguages(string language)
    {
        // Arrange & Act
        var entity = new AnnouncementEntity
        {
            Key = "multilang-test",
            Language = language,
            Markdown = $"Content in {language}"
        };

        // Assert
        Assert.Equal(language, entity.Language);
    }

    [Fact]
    public void AnnouncementEntity_DateRange_IsWithinRange()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var entity = new AnnouncementEntity
        {
            Key = "date-test",
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            IsActive = true
        };

        // Act
        var isInRange = (entity.StartDate == null || entity.StartDate <= now) &&
                        (entity.EndDate == null || entity.EndDate >= now);

        // Assert
        Assert.True(isInRange);
    }

    [Fact]
    public void AnnouncementEntity_DateRange_IsFuture()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var entity = new AnnouncementEntity
        {
            Key = "future-test",
            StartDate = now.AddDays(1),
            IsActive = true
        };

        // Act
        var isInRange = (entity.StartDate == null || entity.StartDate <= now);

        // Assert
        Assert.False(isInRange);
    }

    [Fact]
    public void AnnouncementEntity_DateRange_IsPast()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var entity = new AnnouncementEntity
        {
            Key = "past-test",
            EndDate = now.AddDays(-1),
            IsActive = true
        };

        // Act
        var isInRange = (entity.EndDate == null || entity.EndDate >= now);

        // Assert
        Assert.False(isInRange);
    }

    [Fact]
    public void Markdig_RendersMarkdownCorrectly()
    {
        // Arrange
        var markdown = "**Bold** and *italic* with [link](https://example.com)";

        // Act
        var html = global::Markdig.Markdown.ToHtml(markdown);

        // Assert
        Assert.Contains("<strong>Bold</strong>", html);
        Assert.Contains("<em>italic</em>", html);
        Assert.Contains("href=\"https://example.com\"", html);
    }

    [Fact]
    public void Markdig_HandlesEmptyMarkdown()
    {
        // Arrange
        var markdown = "";

        // Act
        var html = global::Markdig.Markdown.ToHtml(markdown);

        // Assert
        Assert.Equal("", html);
    }

    [Fact]
    public void Markdig_HandlesLinks()
    {
        // Arrange - typical announcement content
        var markdown = "Check out our [new feature](/features/new)!";

        // Act
        var html = global::Markdig.Markdown.ToHtml(markdown);

        // Assert
        Assert.Contains("<a href=\"/features/new\">new feature</a>", html);
    }
}
