using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Xunit;

namespace Mostlylucid.SemanticSearch.Test.Services;

public class HybridSearchServiceTests
{
    private readonly Mock<ILogger<HybridSearchService>> _loggerMock;
    private readonly Mock<ISemanticSearchService> _semanticSearchMock;
    private readonly HybridSearchService _service;

    public HybridSearchServiceTests()
    {
        _loggerMock = new Mock<ILogger<HybridSearchService>>();
        _semanticSearchMock = new Mock<ISemanticSearchService>();

        _service = new HybridSearchService(
            _loggerMock.Object,
            _semanticSearchMock.Object
        );
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsEmptyList()
    {
        // Act
        var results = await _service.SearchAsync("");

        // Assert
        results.Should().BeEmpty();

        _semanticSearchMock.Verify(
            x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SearchAsync_FiltersResultsByLanguage()
    {
        // Arrange
        var query = "test query";
        var language = "en";

        var semanticResults = new List<SearchResult>
        {
            CreateSearchResult("post1", "en", 0.9f),
            CreateSearchResult("post2", "es", 0.85f),
            CreateSearchResult("post3", "en", 0.8f),
            CreateSearchResult("post4", "fr", 0.75f)
        };

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        var results = await _service.SearchAsync(query, language);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Language.Should().Be("en"));
    }

    [Fact]
    public async Task SearchAsync_AppliesReciprocalRankFusion()
    {
        // Arrange
        var query = "test query";
        var language = "en";

        var semanticResults = new List<SearchResult>
        {
            CreateSearchResult("post1", "en", 0.9f),  // Rank 1 in semantic
            CreateSearchResult("post2", "en", 0.8f),  // Rank 2 in semantic
            CreateSearchResult("post3", "en", 0.7f)   // Rank 3 in semantic
        };

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        var results = await _service.SearchAsync(query, language);

        // Assert
        results.Should().HaveCount(3);

        // Results should be ordered by RRF score
        // Higher original ranks should get higher RRF scores
        results[0].Slug.Should().Be("post1");
        results[1].Slug.Should().Be("post2");
        results[2].Slug.Should().Be("post3");
    }

    [Fact]
    public async Task SearchAsync_DeduplicatesBySlugAndLanguage()
    {
        // Arrange
        var query = "test query";
        var language = "en";

        // Simulate same post appearing in results (shouldn't happen but test handling)
        var semanticResults = new List<SearchResult>
        {
            CreateSearchResult("post1", "en", 0.9f),
            CreateSearchResult("post1", "en", 0.8f),  // Duplicate
            CreateSearchResult("post2", "en", 0.7f)
        };

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        var results = await _service.SearchAsync(query, language);

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.Slug).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_RespectsLimitParameter()
    {
        // Arrange
        var query = "test query";
        var language = "en";
        var limit = 2;

        var semanticResults = new List<SearchResult>
        {
            CreateSearchResult("post1", "en", 0.9f),
            CreateSearchResult("post2", "en", 0.8f),
            CreateSearchResult("post3", "en", 0.7f),
            CreateSearchResult("post4", "en", 0.6f),
            CreateSearchResult("post5", "en", 0.5f)
        };

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        var results = await _service.SearchAsync(query, language, limit);

        // Assert
        results.Should().HaveCount(limit);
    }

    [Fact]
    public async Task SearchAsync_RequestsMoreResultsFromSemantic()
    {
        // Arrange
        var query = "test query";
        var language = "en";
        var limit = 10;

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act
        await _service.SearchAsync(query, language, limit);

        // Assert
        // Should request limit * 2 for better fusion
        _semanticSearchMock.Verify(
            x => x.SearchAsync(query, limit * 2, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SearchAsync_HandlesCancellation_ReturnsEmptyList()
    {
        // Arrange
        var query = "test query";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var results = await _service.SearchAsync(query, "en", 10, cts.Token);

        // Assert
        // Service catches exceptions and returns empty list
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_HandlesExceptions_ReturnsEmptyList()
    {
        // Arrange
        var query = "test query";

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var results = await _service.SearchAsync(query);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_UpdatesScoreWithRrfValue()
    {
        // Arrange
        var query = "test query";
        var language = "en";

        var semanticResults = new List<SearchResult>
        {
            CreateSearchResult("post1", "en", 0.95f)  // Original high score
        };

        _semanticSearchMock
            .Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        var results = await _service.SearchAsync(query, language);

        // Assert
        results.Should().HaveCount(1);

        // RRF score should be different from original semantic score
        // RRF score for rank 1: 1 / (60 + 1) = 0.0164
        results[0].Score.Should().NotBe(0.95f);
        results[0].Score.Should().BeApproximately(0.0164f, 0.001f);
    }

    private SearchResult CreateSearchResult(string slug, string language, float score)
    {
        return new SearchResult
        {
            Slug = slug,
            Title = $"Title for {slug}",
            Language = language,
            Score = score,
            Categories = new List<string> { "Test" },
            PublishedDate = DateTime.UtcNow
        };
    }
}
