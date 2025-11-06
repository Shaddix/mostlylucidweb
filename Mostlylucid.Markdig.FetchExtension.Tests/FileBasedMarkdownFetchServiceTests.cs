using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Mostlylucid.Markdig.FetchExtension.Storage;
using System.Net;

namespace Mostlylucid.Markdig.FetchExtension.Tests;

public class FileBasedMarkdownFetchServiceTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<FileBasedMarkdownFetchService>> _mockLogger;
    private readonly string _testCacheDirectory;
    private readonly FileBasedMarkdownFetchService _service;

    public FileBasedMarkdownFetchServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<FileBasedMarkdownFetchService>>();
        _testCacheDirectory = Path.Combine(Path.GetTempPath(), "MarkdownFetchTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testCacheDirectory);
        _service = new FileBasedMarkdownFetchService(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            eventPublisher: null,
            cacheDirectory: _testCacheDirectory);
    }

    [Fact]
    public async Task FetchMarkdownAsync_Success_ReturnsAndCachesContent()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown";
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, content);
        var client = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 1, 0);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be(content);

        // Verify cache file was created
        var cacheFiles = Directory.GetFiles(_testCacheDirectory, "*.json");
        cacheFiles.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchMarkdownAsync_CachedContent_ReturnsCachedVersion()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown";
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, content);
        var client = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // Act - First call should fetch and cache
        await _service.FetchMarkdownAsync(url, 1, 0);

        // Act - Second call should return cached
        var result = await _service.FetchMarkdownAsync(url, 1, 0);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be(content);

        // Verify HTTP client was only called once
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchMarkdownAsync_PersistsAcrossInstances()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown";
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, content);
        var client = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // Act - First service instance fetches
        await _service.FetchMarkdownAsync(url, 1, 0);

        // Create new service instance with same cache directory
        var newService = new FileBasedMarkdownFetchService(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            eventPublisher: null,
            cacheDirectory: _testCacheDirectory);

        // Second instance should read from cache without fetching
        var result = await newService.FetchMarkdownAsync(url, 1, 0);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be(content);

        // HTTP was still only called once (by first instance)
        mockHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchMarkdownAsync_HttpError_ReturnsError()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.NotFound, "");
        var client = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // Act
        var result = await _service.FetchMarkdownAsync(url, 1, 0);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("404");
    }

    [Fact]
    public async Task FetchMarkdownAsync_FetchFailsWithCache_ReturnsStaleCache()
    {
        // Arrange
        var url = "https://example.com/test.md";
        var content = "# Test Markdown";

        // First fetch succeeds
        var mockHandler1 = CreateMockHttpHandler(HttpStatusCode.OK, content);
        var client1 = new HttpClient(mockHandler1.Object);

        // Second fetch fails
        var mockHandler2 = CreateMockHttpHandler(HttpStatusCode.InternalServerError, "");
        var client2 = new HttpClient(mockHandler2.Object);

        _mockHttpClientFactory.SetupSequence(f => f.CreateClient(It.IsAny<string>()))
            .Returns(client1)
            .Returns(client2);

        // Act - First fetch caches content
        await _service.FetchMarkdownAsync(url, 0, 0); // 0 hours = always try to refresh

        // Act - Second fetch fails but should return stale cache
        var result = await _service.FetchMarkdownAsync(url, 0, 0);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be(content);
    }

    public void Dispose()
    {
        // Cleanup test cache directory
        if (Directory.Exists(_testCacheDirectory))
        {
            Directory.Delete(_testCacheDirectory, recursive: true);
        }
    }

    private static Mock<HttpMessageHandler> CreateMockHttpHandler(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return mockHandler;
    }
}
