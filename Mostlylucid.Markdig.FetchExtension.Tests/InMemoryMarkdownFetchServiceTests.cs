using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlylucid.Markdig.FetchExtension.Storage;
using System.Net;

namespace Mostlylucid.Markdig.FetchExtension.Tests;

public class InMemoryMarkdownFetchServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<InMemoryMarkdownFetchService>> _loggerMock;
    private readonly InMemoryMarkdownFetchService _service;

    public InMemoryMarkdownFetchServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<InMemoryMarkdownFetchService>>();
        _service = new InMemoryMarkdownFetchService(_httpClientFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task FetchMarkdownAsync_SuccessfulFetch_ReturnsContent()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown\n\nThis is test content.";
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, content);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 24, 0);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(content, result.Content);
        Assert.NotNull(result.LastRetrieved);
        Assert.False(result.IsCached);
        Assert.False(result.IsStale);
        Assert.Equal(url, result.SourceUrl);
        Assert.Equal(24, result.PollFrequencyHours);
    }

    [Fact]
    public async Task FetchMarkdownAsync_CachedContent_ReturnsCachedResult()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown";
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, content);
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // First fetch to populate cache
        await _service.FetchMarkdownAsync(url, 24, 0);

        // Act - Second fetch should return cached
        var result = await _service.FetchMarkdownAsync(url, 24, 0);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(content, result.Content);
        Assert.True(result.IsCached);
        Assert.False(result.IsStale);
        httpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task FetchMarkdownAsync_StaleCachedContent_FetchesNewContent()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var oldContent = "# Old Content";
        var newContent = "# New Content";

        // First fetch with old content
        var httpMessageHandler1 = CreateMockHttpMessageHandler(HttpStatusCode.OK, oldContent);
        var httpClient1 = new HttpClient(httpMessageHandler1.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient1);
        await _service.FetchMarkdownAsync(url, 0, 0); // pollFrequency=0 means always stale

        // Second fetch with new content
        var httpMessageHandler2 = CreateMockHttpMessageHandler(HttpStatusCode.OK, newContent);
        var httpClient2 = new HttpClient(httpMessageHandler2.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient2);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 0, 0);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(newContent, result.Content);
        Assert.False(result.IsCached);
    }

    [Fact]
    public async Task FetchMarkdownAsync_HttpError_ReturnsStaleCache()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Cached Content";

        // First successful fetch
        var httpMessageHandler1 = CreateMockHttpMessageHandler(HttpStatusCode.OK, content);
        var httpClient1 = new HttpClient(httpMessageHandler1.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient1);
        await _service.FetchMarkdownAsync(url, 0, 0);

        // Second fetch fails
        var httpMessageHandler2 = CreateMockHttpMessageHandler(HttpStatusCode.NotFound, "");
        var httpClient2 = new HttpClient(httpMessageHandler2.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient2);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 0, 0);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(content, result.Content);
        Assert.True(result.IsCached);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task FetchMarkdownAsync_HttpErrorNoCache_ReturnsFailure()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.NotFound, "");
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 24, 0);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("404", result.ErrorMessage);
    }

    [Fact]
    public async Task FetchMarkdownAsync_DifferentBlogPostIds_SeparateCaches()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown";

        // Create a handler that can be called multiple times
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(httpMessageHandler.Object));

        // Act
        var result1 = await _service.FetchMarkdownAsync(url, 24, 1);
        var result2 = await _service.FetchMarkdownAsync(url, 24, 2);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(content, result1.Content);
        Assert.Equal(content, result2.Content);
        // Both should be successful but fetched separately for different blog post IDs
        Assert.False(result1.IsCached); // First fetch
        Assert.False(result2.IsCached); // Second fetch (different cache key)

        // Verify both fetches happened
        httpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task FetchMarkdownAsync_EmptyContent_ReturnsFailure()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK, "");
        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 24, 0);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchMarkdownAsync_Timeout_ReturnsFailure()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var httpMessageHandler = new Mock<HttpMessageHandler>();
        httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(httpMessageHandler.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 24, 0);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static Mock<HttpMessageHandler> CreateMockHttpMessageHandler(HttpStatusCode statusCode, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
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
        return handler;
    }
}
