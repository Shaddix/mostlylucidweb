using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Xunit;

namespace Mostlylucid.SemanticSearch.Test.Services;

public class SemanticSearchServiceTests
{
    private readonly Mock<ILogger<SemanticSearchService>> _loggerMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IVectorStoreService> _vectorStoreServiceMock;
    private readonly SemanticSearchConfig _config;
    private readonly SemanticSearchService _service;

    public SemanticSearchServiceTests()
    {
        _loggerMock = new Mock<ILogger<SemanticSearchService>>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorStoreServiceMock = new Mock<IVectorStoreService>();

        _config = new SemanticSearchConfig
        {
            Enabled = true,
            VectorSize = 384,
            SearchResultsCount = 10,
            RelatedPostsCount = 5,
            MinimumSimilarityScore = 0.5f
        };

        _service = new SemanticSearchService(
            _loggerMock.Object,
            _config,
            _embeddingServiceMock.Object,
            _vectorStoreServiceMock.Object
        );
    }

    [Fact]
    public async Task InitializeAsync_WhenEnabled_InitializesVectorStore()
    {
        // Act
        await _service.InitializeAsync();

        // Assert
        _vectorStoreServiceMock.Verify(
            x => x.InitializeCollectionAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task InitializeAsync_WhenDisabled_DoesNotInitialize()
    {
        // Arrange
        var disabledConfig = new SemanticSearchConfig { Enabled = false };
        var disabledService = new SemanticSearchService(
            _loggerMock.Object,
            disabledConfig,
            _embeddingServiceMock.Object,
            _vectorStoreServiceMock.Object
        );

        // Act
        await disabledService.InitializeAsync();

        // Assert
        _vectorStoreServiceMock.Verify(
            x => x.InitializeCollectionAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task IndexPostAsync_GeneratesEmbeddingAndStores()
    {
        // Arrange
        var document = CreateTestDocument();
        var embedding = new float[384];

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        await _service.IndexPostAsync(document);

        // Assert
        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        _vectorStoreServiceMock.Verify(
            x => x.IndexDocumentAsync(document, embedding, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task IndexPostAsync_ComputesContentHashWhenNotProvided()
    {
        // Arrange
        var document = CreateTestDocument();
        document.ContentHash = null;

        var embedding = new float[384];
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        await _service.IndexPostAsync(document);

        // Assert
        document.ContentHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task IndexPostsAsync_IndexesMultipleDocuments()
    {
        // Arrange
        var documents = new[]
        {
            CreateTestDocument("doc1", "Title 1", "Content 1"),
            CreateTestDocument("doc2", "Title 2", "Content 2"),
            CreateTestDocument("doc3", "Title 3", "Content 3")
        };

        var embedding = new float[384];
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        // Act
        await _service.IndexPostsAsync(documents);

        // Assert
        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );

        _vectorStoreServiceMock.Verify(
            x => x.IndexDocumentsAsync(
                It.Is<IEnumerable<(BlogPostDocument, float[])>>(docs => docs.Count() == 3),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SearchAsync_GeneratesQueryEmbeddingAndSearches()
    {
        // Arrange
        var query = "test query";
        var queryEmbedding = new float[384];
        var expectedResults = new List<SearchResult>
        {
            new SearchResult
            {
                Slug = "test-post",
                Title = "Test Post",
                Language = "en",
                Score = 0.9f,
                Categories = new List<string> { "Test" },
                PublishedDate = DateTime.UtcNow
            }
        };

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _vectorStoreServiceMock
            .Setup(x => x.SearchAsync(
                queryEmbedding,
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await _service.SearchAsync(query);

        // Assert
        results.Should().HaveCount(1);
        results[0].Slug.Should().Be("test-post");

        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()),
            Times.Once
        );

        _vectorStoreServiceMock.Verify(
            x => x.SearchAsync(
                queryEmbedding,
                _config.SearchResultsCount,
                _config.MinimumSimilarityScore,
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ReturnsEmptyList()
    {
        // Act
        var results = await _service.SearchAsync("");

        // Assert
        results.Should().BeEmpty();

        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetRelatedPostsAsync_CallsVectorStore()
    {
        // Arrange
        var slug = "test-post";
        var language = "en";
        var expectedResults = new List<SearchResult>
        {
            new SearchResult
            {
                Slug = "related-post",
                Title = "Related Post",
                Language = "en",
                Score = 0.8f,
                Categories = new List<string> { "Test" },
                PublishedDate = DateTime.UtcNow
            }
        };

        _vectorStoreServiceMock
            .Setup(x => x.FindRelatedPostsAsync(slug, language, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await _service.GetRelatedPostsAsync(slug, language);

        // Assert
        results.Should().HaveCount(1);
        results[0].Slug.Should().Be("related-post");

        _vectorStoreServiceMock.Verify(
            x => x.FindRelatedPostsAsync(slug, language, _config.RelatedPostsCount, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task DeletePostAsync_CallsVectorStore()
    {
        // Arrange
        var slug = "test-post";
        var language = "en";

        // Act
        await _service.DeletePostAsync(slug, language);

        // Assert
        _vectorStoreServiceMock.Verify(
            x => x.DeleteDocumentAsync($"{slug}_{language}", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task NeedsReindexingAsync_WhenHashDifferent_ReturnsTrue()
    {
        // Arrange
        var slug = "test-post";
        var language = "en";
        var currentHash = "newhash123";
        var existingHash = "oldhash456";

        _vectorStoreServiceMock
            .Setup(x => x.GetDocumentHashAsync($"{slug}_{language}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingHash);

        // Act
        var result = await _service.NeedsReindexingAsync(slug, language, currentHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NeedsReindexingAsync_WhenHashSame_ReturnsFalse()
    {
        // Arrange
        var slug = "test-post";
        var language = "en";
        var currentHash = "samehash123";

        _vectorStoreServiceMock
            .Setup(x => x.GetDocumentHashAsync($"{slug}_{language}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentHash);

        // Act
        var result = await _service.NeedsReindexingAsync(slug, language, currentHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NeedsReindexingAsync_WhenDocumentNotExists_ReturnsTrue()
    {
        // Arrange
        var slug = "test-post";
        var language = "en";
        var currentHash = "hash123";

        _vectorStoreServiceMock
            .Setup(x => x.GetDocumentHashAsync($"{slug}_{language}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.NeedsReindexingAsync(slug, language, currentHash);

        // Assert
        result.Should().BeTrue();
    }

    private BlogPostDocument CreateTestDocument(string? id = null, string? title = null, string? content = null)
    {
        return new BlogPostDocument
        {
            Id = id ?? "test-post_en",
            Slug = "test-post",
            Title = title ?? "Test Post",
            Content = content ?? "This is test content for the blog post.",
            Language = "en",
            Categories = new List<string> { "Test", "Sample" },
            PublishedDate = DateTime.UtcNow,
            ContentHash = null
        };
    }
}
