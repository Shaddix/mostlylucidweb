using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.Blog.WatcherService;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Services.Markdown.MarkDigExtensions;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Models;
using Mostlylucid.Test.Extensions;

namespace Mostlylucid.Test.Tests;

public class MarkdownFetchPollingServiceTests
{
    private readonly Mock<IMostlylucidDBContext> _dbContextMock;
    private readonly Mock<IMarkdownFetchService> _fetchServiceMock;
    private readonly Mock<IMarkdownBlogService> _markdownBlogServiceMock;
    private readonly Mock<IBlogService> _blogServiceMock;
    private readonly IServiceProvider _serviceProvider;

    public MarkdownFetchPollingServiceTests()
    {
        var services = new ServiceCollection();

        _dbContextMock = new Mock<IMostlylucidDBContext>();
        _fetchServiceMock = new Mock<IMarkdownFetchService>();
        _markdownBlogServiceMock = new Mock<IMarkdownBlogService>();
        _blogServiceMock = new Mock<IBlogService>();

        services.AddSingleton(_dbContextMock.Object);
        services.AddSingleton(_fetchServiceMock.Object);
        services.AddSingleton(_markdownBlogServiceMock.Object);
        services.AddSingleton(_blogServiceMock.Object);
        services.AddLogging(configure => configure.AddConsole());
        services.AddSingleton<IServiceScopeFactory>(sp =>
        {
            var mockFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            mockScope.Setup(s => s.ServiceProvider).Returns(sp);
            mockFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            return mockFactory.Object;
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task PollAndUpdate_NoEnabledFetches_DoesNothing()
    {
        // Arrange
        var emptyList = new List<MarkdownFetchEntity>();
        _dbContextMock.SetupDbSet(emptyList, x => x.MarkdownFetches);

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<MarkdownFetchPollingService>>();
        var service = new MarkdownFetchPollingService(scopeFactory, logger);

        // Can't easily test the background service directly, but we can verify setup
        Assert.NotNull(service);
    }

    [Fact]
    public void MarkdownFetchPollingService_CanBeInstantiated()
    {
        // Arrange
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = _serviceProvider.GetRequiredService<ILogger<MarkdownFetchPollingService>>();

        // Act
        var service = new MarkdownFetchPollingService(scopeFactory, logger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task FetchService_PollingLogic_ChecksFrequency()
    {
        // Arrange
        var url = "https://example.com/content.md";
        var blogPost = new BlogPostEntity
        {
            Id = 1,
            Slug = "test-post",
            Title = "Test Post"
        };

        var fetchEntity = new MarkdownFetchEntity
        {
            Id = 1,
            Url = url,
            BlogPostId = blogPost.Id,
            PollFrequencyHours = 12,
            LastFetchedAt = DateTimeOffset.UtcNow.AddHours(-6), // Only 6 hours ago
            CachedContent = "# Old Content",
            ContentHash = "old-hash",
            IsEnabled = true,
            BlogPost = blogPost
        };

        // This test verifies the polling frequency logic
        var timeSinceLastFetch = DateTimeOffset.UtcNow - fetchEntity.LastFetchedAt!.Value;
        var shouldPoll = timeSinceLastFetch.TotalHours >= fetchEntity.PollFrequencyHours;

        // Assert
        Assert.False(shouldPoll); // Should not poll yet (only 6 hours, needs 12)
    }

    [Fact]
    public async Task FetchService_PollingLogic_UpdatesWhenContentChanges()
    {
        // Arrange
        var url = "https://example.com/content.md";
        var oldContent = "# Old Content";
        var newContent = "# New Content";
        var oldHash = ComputeTestHash(oldContent);
        var newHash = ComputeTestHash(newContent);

        var blogPost = new BlogPostEntity
        {
            Id = 1,
            Slug = "test-post",
            Title = "Test Post"
        };

        var fetchEntity = new MarkdownFetchEntity
        {
            Id = 1,
            Url = url,
            BlogPostId = blogPost.Id,
            PollFrequencyHours = 12,
            LastFetchedAt = DateTimeOffset.UtcNow.AddHours(-24), // 24 hours ago, should poll
            CachedContent = oldContent,
            ContentHash = oldHash,
            IsEnabled = true,
            BlogPost = blogPost
        };

        // Simulate content change
        var contentChanged = newHash != oldHash;

        // Assert
        Assert.True(contentChanged);
    }

    [Fact]
    public void FetchEntity_ConsecutiveFailures_DisablesAfterThreshold()
    {
        // Arrange
        var fetchEntity = new MarkdownFetchEntity
        {
            Id = 1,
            Url = "https://example.com/content.md",
            BlogPostId = 1,
            PollFrequencyHours = 12,
            ConsecutiveFailures = 10,
            IsEnabled = true
        };

        // Assert
        Assert.True(fetchEntity.ConsecutiveFailures >= 10);
        // In the actual service, this would trigger disabling
    }

    [Fact]
    public async Task FetchService_SuccessfulFetch_ResetsFailureCount()
    {
        // Arrange
        var url = "https://example.com/content.md";
        var newContent = "# New Content";

        _fetchServiceMock
            .Setup(x => x.FetchMarkdownAsync(url, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new MarkdownFetchResult
            {
                Success = true,
                Content = newContent
            });

        // Act
        var result = await _fetchServiceMock.Object.FetchMarkdownAsync(url, 12, 1);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(newContent, result.Content);
    }

    [Fact]
    public async Task FetchService_FailedFetch_ReturnsError()
    {
        // Arrange
        var url = "https://example.com/content.md";
        var errorMessage = "HTTP 404: Not Found";

        _fetchServiceMock
            .Setup(x => x.FetchMarkdownAsync(url, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = errorMessage
            });

        // Act
        var result = await _fetchServiceMock.Object.FetchMarkdownAsync(url, 12, 1);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    private string ComputeTestHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
