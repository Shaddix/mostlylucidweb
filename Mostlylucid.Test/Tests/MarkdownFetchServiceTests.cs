using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Test.Extensions;
using System.Net;

namespace Mostlylucid.Test.Tests;

public class MarkdownFetchServiceTests
{
    private readonly Mock<IMostlylucidDBContext> _dbContextMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<MarkdownFetchService>> _loggerMock;
    private readonly IServiceProvider _serviceProvider;

    public MarkdownFetchServiceTests()
    {
        var services = new ServiceCollection();
        _dbContextMock = new Mock<IMostlylucidDBContext>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<MarkdownFetchService>>();

        services.AddSingleton(_dbContextMock.Object);
        services.AddSingleton(_httpClientFactoryMock.Object);
        services.AddSingleton(_loggerMock.Object);
        // Add BlogPostProcessingContext - required by MarkdownFetchService
        services.AddScoped<Mostlylucid.Services.Blog.BlogPostProcessingContext>();
        services.AddScoped<IMarkdownFetchService, MarkdownFetchService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    private Mock<HttpMessageHandler> CreateMockHttpMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return handlerMock;
    }

    [Fact]
    public async Task FetchMarkdownAsync_NewUrl_FetchesAndCaches()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown\n\nThis is a test.";
        var pollFrequency = 12;
        var blogPostId = 1;

        var fetchEntities = new List<MarkdownFetchEntity>();
        _dbContextMock.SetupDbSet(fetchEntities, x => x.MarkdownFetches);

        var handlerMock = CreateMockHttpMessageHandler(content);
        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = _serviceProvider.GetRequiredService<IMarkdownFetchService>();

        // Act
        var result = await service.FetchMarkdownAsync(url, pollFrequency, blogPostId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(content, result.Content);
        Assert.Null(result.ErrorMessage);

        // Verify entity was added
        _dbContextMock.Verify(x => x.MarkdownFetches.Add(It.Is<MarkdownFetchEntity>(
            e => e.Url == url && e.PollFrequencyHours == pollFrequency && e.BlogPostId == blogPostId
        )), Times.Once);

        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchMarkdownAsync_CachedContent_ReturnsCached()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var cachedContent = "# Cached Content";
        var pollFrequency = 12;
        var blogPostId = 1;

        var fetchEntity = new MarkdownFetchEntity
        {
            Url = url,
            BlogPostId = blogPostId,
            PollFrequencyHours = pollFrequency,
            CachedContent = cachedContent,
            LastFetchedAt = DateTimeOffset.UtcNow.AddHours(-6), // 6 hours ago, still fresh
            ContentHash = "some-hash",
            IsEnabled = true
        };

        var fetchEntities = new List<MarkdownFetchEntity> { fetchEntity };
        _dbContextMock.SetupDbSet(fetchEntities, x => x.MarkdownFetches);

        var service = _serviceProvider.GetRequiredService<IMarkdownFetchService>();

        // Act
        var result = await service.FetchMarkdownAsync(url, pollFrequency, blogPostId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(cachedContent, result.Content);

        // Verify no HTTP call was made
        _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FetchMarkdownAsync_HttpError_ReturnsFailure()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var pollFrequency = 12;
        var blogPostId = 1;

        var fetchEntities = new List<MarkdownFetchEntity>();
        _dbContextMock.SetupDbSet(fetchEntities, x => x.MarkdownFetches);

        var handlerMock = CreateMockHttpMessageHandler("Not Found", HttpStatusCode.NotFound);
        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = _serviceProvider.GetRequiredService<IMarkdownFetchService>();

        // Act
        var result = await service.FetchMarkdownAsync(url, pollFrequency, blogPostId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("404", result.ErrorMessage);
    }

    [Fact]
    public async Task FetchMarkdownAsync_HttpError_WithCachedContent_ReturnsStaleCache()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var cachedContent = "# Cached Content";
        var pollFrequency = 12;
        var blogPostId = 1;

        var fetchEntity = new MarkdownFetchEntity
        {
            Id = 1,
            Url = url,
            BlogPostId = blogPostId,
            PollFrequencyHours = pollFrequency,
            CachedContent = cachedContent,
            LastFetchedAt = DateTimeOffset.UtcNow.AddHours(-24), // 24 hours ago, stale
            ContentHash = "some-hash",
            IsEnabled = true
        };

        var fetchEntities = new List<MarkdownFetchEntity> { fetchEntity };
        _dbContextMock.SetupDbSet(fetchEntities, x => x.MarkdownFetches);

        var handlerMock = CreateMockHttpMessageHandler("Error", HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = _serviceProvider.GetRequiredService<IMarkdownFetchService>();

        // Act
        var result = await service.FetchMarkdownAsync(url, pollFrequency, blogPostId);

        // Assert
        Assert.True(result.Success); // Returns success with stale cache
        Assert.Equal(cachedContent, result.Content);

        // Verify consecutive failures incremented
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchMarkdownAsync_EmptyContent_ReturnsFailure()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var pollFrequency = 12;
        var blogPostId = 1;

        var fetchEntities = new List<MarkdownFetchEntity>();
        _dbContextMock.SetupDbSet(fetchEntities, x => x.MarkdownFetches);

        var handlerMock = CreateMockHttpMessageHandler(""); // Empty content
        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = _serviceProvider.GetRequiredService<IMarkdownFetchService>();

        // Act
        var result = await service.FetchMarkdownAsync(url, pollFrequency, blogPostId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("empty", result.ErrorMessage.ToLower());
    }

    [Fact]
    public async Task FetchMarkdownAsync_UpdatesContentHash()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var newContent = "# Updated Content";
        var pollFrequency = 12;
        var blogPostId = 1;

        var fetchEntity = new MarkdownFetchEntity
        {
            Id = 1,
            Url = url,
            BlogPostId = blogPostId,
            PollFrequencyHours = pollFrequency,
            CachedContent = "# Old Content",
            LastFetchedAt = DateTimeOffset.UtcNow.AddHours(-24),
            ContentHash = "old-hash",
            IsEnabled = true
        };

        var fetchEntities = new List<MarkdownFetchEntity> { fetchEntity };
        _dbContextMock.SetupDbSet(fetchEntities, x => x.MarkdownFetches);

        var handlerMock = CreateMockHttpMessageHandler(newContent);
        var httpClient = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = _serviceProvider.GetRequiredService<IMarkdownFetchService>();

        // Act
        var result = await service.FetchMarkdownAsync(url, pollFrequency, blogPostId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(newContent, result.Content);
        Assert.NotEqual("old-hash", fetchEntity.ContentHash);
        Assert.Equal(0, fetchEntity.ConsecutiveFailures);
        Assert.Null(fetchEntity.LastError);
    }
}
