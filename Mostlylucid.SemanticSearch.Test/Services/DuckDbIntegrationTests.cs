using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Xunit.Abstractions;

namespace Mostlylucid.SemanticSearch.Test.Services;

/// <summary>
/// Integration tests for DuckDB vector store backend
/// </summary>
public class DuckDbIntegrationTests : IAsyncDisposable
{
    private readonly string _testDbPath;
    private readonly DuckDbVectorStoreService _vectorStore;
    private readonly DocSummarizerEmbeddingService _embeddingService;
    private readonly SemanticSearchService _searchService;
    private readonly ITestOutputHelper _output;

    public DuckDbIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Use a unique temp file for each test run
        _testDbPath = Path.Combine(Path.GetTempPath(), $"semantic-search-test-{Guid.NewGuid()}.duckdb");
        _output.WriteLine($"Test DB Path: {_testDbPath}");
        
        var config = new SemanticSearchConfig
        {
            Enabled = true,
            Backend = VectorStoreBackend.DuckDB,
            DuckDbPath = _testDbPath,
            CollectionName = "test_posts",
            VectorSize = 384,
            MinimumSimilarityScore = 0.3f,
            SearchResultsCount = 10,
            RelatedPostsCount = 5
        };

        var vectorStoreLogger = Mock.Of<ILogger<DuckDbVectorStoreService>>();
        var embeddingLogger = Mock.Of<ILogger<DocSummarizerEmbeddingService>>();
        var searchLogger = Mock.Of<ILogger<SemanticSearchService>>();

        _embeddingService = new DocSummarizerEmbeddingService(embeddingLogger, config);
        _vectorStore = new DuckDbVectorStoreService(vectorStoreLogger, config);
        _searchService = new SemanticSearchService(searchLogger, config, _embeddingService, _vectorStore);
    }

    public async ValueTask DisposeAsync()
    {
        await _vectorStore.DisposeAsync();
        _embeddingService.Dispose();
        
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { /* ignore */ }
        }
        if (File.Exists(_testDbPath + ".wal"))
        {
            try { File.Delete(_testDbPath + ".wal"); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesCollectionSuccessfully()
    {
        // Act
        await _searchService.InitializeAsync();
        
        // Assert - no exception means success
    }

    [Fact]
    public async Task IndexAndSearch_WithSamplePosts_ReturnsRelevantResults()
    {
        // Arrange
        await _searchService.InitializeAsync();
        
        var posts = new List<BlogPostDocument>
        {
            new()
            {
                Id = "wpf-desktop-development",
                Slug = "wpf-desktop-development",
                Title = "WPF for the Web Developer - A Learning Journey",
                Content = "Building desktop applications with WPF and Entity Framework. Learning about XAML, data binding, and SQLite for local storage.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "docker-containers-guide",
                Slug = "docker-containers-guide",
                Title = "Docker Containers Deep Dive",
                Content = "Understanding containerization with Docker. Creating Dockerfiles, managing images, and orchestrating with docker-compose.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "semantic-search-implementation",
                Slug = "semantic-search-implementation",
                Title = "Implementing Semantic Search with ONNX",
                Content = "Building semantic search using embeddings and vector databases. Using all-MiniLM model for text embeddings.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "entity-framework-tips",
                Slug = "entity-framework-tips",
                Title = "Entity Framework Best Practices",
                Content = "Tips and tricks for Entity Framework Core. Optimizing queries, using migrations, and handling concurrency.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "aspnet-core-api",
                Slug = "aspnet-core-api",
                Title = "Building REST APIs with ASP.NET Core",
                Content = "Creating robust REST APIs using ASP.NET Core. Authentication, validation, and best practices.",
                Languages = new[] { "en" }
            }
        };

        // Act - Index all posts
        await _searchService.IndexPostsAsync(posts);

        // Verify documents were stored
        var storedHash = await _vectorStore.GetDocumentHashAsync("docker-containers-guide");
        storedHash.Should().NotBeNull("document should be stored in vector store");

        // Search for docker-related content
        var dockerResults = await _searchService.SearchAsync("docker containers deployment");
        
        // Search for database-related content
        var dbResults = await _searchService.SearchAsync("database Entity Framework SQL");
        
        // Search for embeddings/ML content
        var mlResults = await _searchService.SearchAsync("semantic search embeddings vector");

        // Assert - check results contain expected content (don't require exact ordering, 
        // as semantic similarity can vary based on embedding model and short text snippets)
        dockerResults.Should().NotBeEmpty("docker search should return results");
        dockerResults.Should().Contain(r => r.Slug == "docker-containers-guide", 
            "docker-related post should be in search results");
        
        dbResults.Should().NotBeEmpty("database search should return results");
        dbResults.Should().Contain(r => r.Slug == "entity-framework-tips" || r.Slug == "wpf-desktop-development",
            "database-related posts should be in search results");
        
        mlResults.Should().NotBeEmpty("ML/embedding search should return results");
        mlResults.Should().Contain(r => r.Slug == "semantic-search-implementation",
            "semantic search post should be in search results");
    }

    [Fact]
    public async Task GetRelatedPosts_ReturnsSemanticallySimilarPosts()
    {
        // Arrange
        await _searchService.InitializeAsync();
        
        var posts = new List<BlogPostDocument>
        {
            new()
            {
                Id = "csharp-async-patterns",
                Slug = "csharp-async-patterns",
                Title = "Async/Await Patterns in C#",
                Content = "Understanding asynchronous programming in C#. Task, ValueTask, async streams, and cancellation tokens.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "csharp-parallel-programming",
                Slug = "csharp-parallel-programming",
                Title = "Parallel Programming in C#",
                Content = "Multi-threading and parallel processing in C#. Parallel.ForEach, PLINQ, and thread synchronization.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "javascript-promises",
                Slug = "javascript-promises",
                Title = "JavaScript Promises and Async",
                Content = "Asynchronous JavaScript with Promises and async/await. Event loop and non-blocking I/O.",
                Languages = new[] { "en" }
            },
            new()
            {
                Id = "python-machine-learning",
                Slug = "python-machine-learning",
                Title = "Machine Learning with Python",
                Content = "Building ML models with scikit-learn and TensorFlow. Neural networks and deep learning.",
                Languages = new[] { "en" }
            }
        };

        await _searchService.IndexPostsAsync(posts);

        // Act - Find posts related to the async C# post
        var relatedPosts = await _searchService.GetRelatedPostsAsync("csharp-async-patterns", limit: 3);

        // Assert
        relatedPosts.Should().NotBeEmpty();
        // The parallel programming post should be highly related (both C# concurrency topics)
        relatedPosts.Should().Contain(r => r.Slug == "csharp-parallel-programming");
    }

    [Fact]
    public async Task DeletePost_RemovesFromIndex()
    {
        // Arrange
        await _searchService.InitializeAsync();
        
        var post = new BlogPostDocument
        {
            Id = "test-post-to-delete",
            Slug = "test-post-to-delete",
            Title = "Test Post",
            Content = "This is a unique test post about quantum computing and blockchain.",
            Languages = new[] { "en" }
        };

        await _searchService.IndexPostAsync(post);
        
        // Verify it's indexed
        var beforeDelete = await _searchService.SearchAsync("quantum computing blockchain");
        beforeDelete.Should().Contain(r => r.Slug == "test-post-to-delete");

        // Act
        await _searchService.DeletePostAsync("test-post-to-delete");

        // Assert
        var afterDelete = await _searchService.SearchAsync("quantum computing blockchain");
        afterDelete.Should().NotContain(r => r.Slug == "test-post-to-delete");
    }

    [Fact]
    public async Task NeedsReindexing_DetectsContentChanges()
    {
        // Arrange
        await _searchService.InitializeAsync();
        
        var post = new BlogPostDocument
        {
            Id = "content-hash-test",
            Slug = "content-hash-test",
            Title = "Hash Test Post",
            Content = "Original content for hash testing.",
            ContentHash = "original-hash-123",
            Languages = new[] { "en" }
        };

        await _searchService.IndexPostAsync(post);

        // Act & Assert
        var needsReindex1 = await _searchService.NeedsReindexingAsync("content-hash-test", "original-hash-123");
        needsReindex1.Should().BeFalse("same hash should not need reindexing");

        var needsReindex2 = await _searchService.NeedsReindexingAsync("content-hash-test", "different-hash-456");
        needsReindex2.Should().BeTrue("different hash should need reindexing");

        var needsReindex3 = await _searchService.NeedsReindexingAsync("non-existent-post", "any-hash");
        needsReindex3.Should().BeTrue("non-existent post should need indexing");
    }
}
