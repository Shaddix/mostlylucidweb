using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;
using Xunit;

namespace Mostlylucid.DocSummarizer.Core.Tests.Services;

/// <summary>
/// Unit tests for DuckDbVectorStore to ensure proper storage and retrieval of segments.
/// These tests verify the critical data path that was previously untested.
/// </summary>
public class DuckDbVectorStoreTests : IAsyncDisposable
{
    private readonly string _testDbPath;
    private readonly DuckDbVectorStore _store;
    private const int VectorDimension = 384;
    private const string TestCollection = "test_collection";

    public DuckDbVectorStoreTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"duckdb-test-{Guid.NewGuid()}.duckdb");
        _store = new DuckDbVectorStore(_testDbPath, VectorDimension, verbose: false);
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        
        // Clean up test database files
        try { File.Delete(_testDbPath); } catch { }
        try { File.Delete(_testDbPath + ".wal"); } catch { }
    }

    #region Initialization Tests

    [Fact]
    public async Task InitializeAsync_CreatesDatabase_Successfully()
    {
        // Act
        await _store.InitializeAsync(TestCollection, VectorDimension);

        // Assert - should not throw and file should exist
        Assert.True(File.Exists(_testDbPath));
    }

    [Fact]
    public async Task InitializeAsync_CanBeCalledMultipleTimes_Safely()
    {
        // Act - call multiple times
        await _store.InitializeAsync(TestCollection, VectorDimension);
        await _store.InitializeAsync(TestCollection, VectorDimension);
        await _store.InitializeAsync(TestCollection, VectorDimension);

        // Assert - should not throw
        Assert.True(File.Exists(_testDbPath));
    }

    #endregion

    #region Upsert and Retrieval Tests

    [Fact]
    public async Task UpsertSegmentsAsync_SingleSegment_CanBeRetrieved()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var embedding = CreateTestEmbedding(VectorDimension);
        var segment = new Segment("test-doc", "Test content for embedding", SegmentType.Sentence, 0, 0, 100)
        {
            SectionTitle = "Test Section",
            HeadingLevel = 2,
            SalienceScore = 0.8,
            Embedding = embedding
        };

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, new[] { segment });
        var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, "test_doc");

        // Assert
        Assert.Single(retrieved);
        Assert.Equal("Test content for embedding", retrieved[0].Text);
        Assert.Equal("Test Section", retrieved[0].SectionTitle);
        Assert.Equal(2, retrieved[0].HeadingLevel);
        Assert.Equal(0.8, retrieved[0].SalienceScore, 2);
        Assert.NotNull(retrieved[0].Embedding);
        Assert.Equal(VectorDimension, retrieved[0].Embedding!.Length);
    }

    [Fact]
    public async Task UpsertSegmentsAsync_MultipleSegments_AllCanBeRetrieved()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segments = new List<Segment>();
        for (int i = 0; i < 5; i++)
        {
            var segment = new Segment("multi-doc", $"Content {i}", SegmentType.Sentence, i, i * 10, (i + 1) * 10)
            {
                SalienceScore = 0.5 + (i * 0.1),
                Embedding = CreateTestEmbedding(VectorDimension, seed: i)
            };
            segments.Add(segment);
        }

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, segments);
        var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, "multi_doc");

        // Assert
        Assert.Equal(5, retrieved.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal($"Content {i}", retrieved[i].Text);
        }
    }

    [Fact]
    public async Task UpsertSegmentsAsync_UpdateExisting_OverwritesData()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var original = new Segment("update-doc", "Original content", SegmentType.Sentence, 0, 0, 50)
        {
            SalienceScore = 0.5,
            Embedding = CreateTestEmbedding(VectorDimension)
        };

        var updated = new Segment("update-doc", "Updated content", SegmentType.Sentence, 0, 0, 50)
        {
            SalienceScore = 0.9,
            Embedding = CreateTestEmbedding(VectorDimension, seed: 42)
        };

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, new[] { original });
        await _store.UpsertSegmentsAsync(TestCollection, new[] { updated });
        var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, "update_doc");

        // Assert - should have updated content
        Assert.Single(retrieved);
        Assert.Equal("Updated content", retrieved[0].Text);
        Assert.Equal(0.9, retrieved[0].SalienceScore, 2);
    }

    [Fact]
    public async Task UpsertSegmentsAsync_PreservesContentHash()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segment = new Segment("hash-doc", "Content with hash", SegmentType.Sentence, 0, 0, 50)
        {
            Embedding = CreateTestEmbedding(VectorDimension)
        };
        var originalHash = segment.ContentHash;

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, new[] { segment });
        var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, "hash_doc");

        // Assert
        Assert.Single(retrieved);
        Assert.Equal(originalHash, retrieved[0].ContentHash);
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task SearchAsync_ReturnsResults_WithSimilarityScores()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segments = new[]
        {
            new Segment("doc1", "First document", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 1)
            },
            new Segment("doc2", "Second document", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 2)
            },
            new Segment("doc3", "Third document", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 3)
            }
        };
        await _store.UpsertSegmentsAsync(TestCollection, segments);

        // Act - search with embedding similar to doc1
        var queryEmbedding = CreateTestEmbedding(VectorDimension, seed: 1);
        var results = await _store.SearchAsync(TestCollection, queryEmbedding, topK: 3);

        // Assert
        Assert.Equal(3, results.Count);
        // First result should be most similar (same seed = identical embedding = highest similarity)
        Assert.Equal("First document", results[0].Text);
        Assert.True(results[0].QuerySimilarity > 0.9, "Identical embedding should have high similarity");
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK_Limit()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segments = new List<Segment>();
        for (int i = 0; i < 10; i++)
        {
            segments.Add(new Segment($"doc{i}", $"Document {i}", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: i)
            });
        }
        await _store.UpsertSegmentsAsync(TestCollection, segments);

        // Act
        var queryEmbedding = CreateTestEmbedding(VectorDimension, seed: 0);
        var results = await _store.SearchAsync(TestCollection, queryEmbedding, topK: 3);

        // Assert
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchAsync_EmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);

        // Act
        var queryEmbedding = CreateTestEmbedding(VectorDimension);
        var results = await _store.SearchAsync(TestCollection, queryEmbedding, topK: 10);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteDocumentAsync_RemovesAllSegments()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segments = new[]
        {
            new Segment("delete-doc", "Segment 1", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 1)
            },
            new Segment("delete-doc", "Segment 2", SegmentType.Sentence, 1, 50, 100)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 2)
            }
        };
        await _store.UpsertSegmentsAsync(TestCollection, segments);

        // Verify they exist
        var beforeDelete = await _store.GetDocumentSegmentsAsync(TestCollection, "delete_doc");
        Assert.Equal(2, beforeDelete.Count);

        // Act
        await _store.DeleteDocumentAsync(TestCollection, "delete_doc");

        // Assert
        var afterDelete = await _store.GetDocumentSegmentsAsync(TestCollection, "delete_doc");
        Assert.Empty(afterDelete);
    }

    [Fact]
    public async Task DeleteDocumentAsync_OnlyAffectsSpecifiedDocument()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        await _store.UpsertSegmentsAsync(TestCollection, new[]
        {
            new Segment("keep-doc", "Keep this", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 1)
            },
            new Segment("delete-doc", "Delete this", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 2)
            }
        });

        // Act
        await _store.DeleteDocumentAsync(TestCollection, "delete_doc");

        // Assert
        var keepDoc = await _store.GetDocumentSegmentsAsync(TestCollection, "keep_doc");
        var deleteDoc = await _store.GetDocumentSegmentsAsync(TestCollection, "delete_doc");
        
        Assert.Single(keepDoc);
        Assert.Empty(deleteDoc);
    }

    #endregion

    #region Collection Management Tests

    [Fact]
    public async Task DeleteCollectionAsync_RemovesAllDocuments()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        await _store.UpsertSegmentsAsync(TestCollection, new[]
        {
            new Segment("doc1", "Document 1", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 1)
            },
            new Segment("doc2", "Document 2", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 2)
            }
        });

        // Act
        await _store.DeleteCollectionAsync(TestCollection);

        // Assert
        var doc1 = await _store.GetDocumentSegmentsAsync(TestCollection, "doc1");
        var doc2 = await _store.GetDocumentSegmentsAsync(TestCollection, "doc2");
        Assert.Empty(doc1);
        Assert.Empty(doc2);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        await _store.UpsertSegmentsAsync(TestCollection, new[]
        {
            new Segment("doc1", "Content 1", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 1)
            },
            new Segment("doc1", "Content 2", SegmentType.Sentence, 1, 50, 100)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 2)
            },
            new Segment("doc2", "Content 3", SegmentType.Sentence, 0, 0, 50)
            {
                Embedding = CreateTestEmbedding(VectorDimension, seed: 3)
            }
        });

        // Act
        var (segmentCount, collectionCount, cacheCount, dbSize) = await _store.GetStatsAsync();

        // Assert
        Assert.Equal(3, segmentCount);
        Assert.Equal(1, collectionCount); // All in same collection
        Assert.True(dbSize > 0);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetDocumentSegmentsAsync_NonexistentDocument_ReturnsEmptyList()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);

        // Act
        var results = await _store.GetDocumentSegmentsAsync(TestCollection, "nonexistent_doc");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task UpsertSegmentsAsync_EmptyList_DoesNotThrow()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);

        // Act & Assert - should not throw
        await _store.UpsertSegmentsAsync(TestCollection, Array.Empty<Segment>());
    }

    [Fact]
    public async Task UpsertSegmentsAsync_SegmentWithoutEmbedding_StillStored()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segment = new Segment("no-embed-doc", "No embedding content", SegmentType.Sentence, 0, 0, 50)
        {
            Embedding = null
        };

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, new[] { segment });
        var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, "no_embed_doc");

        // Assert
        Assert.Single(retrieved);
        Assert.Equal("No embedding content", retrieved[0].Text);
        Assert.Null(retrieved[0].Embedding);
    }

    [Fact]
    public async Task Segment_DocIdSanitization_WorksCorrectly()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        // DocId with hyphens should be sanitized to underscores
        var segment = new Segment("my-complex-doc-id", "Content", SegmentType.Sentence, 0, 0, 50)
        {
            Embedding = CreateTestEmbedding(VectorDimension)
        };

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, new[] { segment });
        
        // Should be able to retrieve with sanitized doc_id
        var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, "my_complex_doc_id");

        // Assert
        Assert.Single(retrieved);
    }

    [Fact]
    public async Task UpsertSegmentsAsync_AllSegmentTypes_StoredCorrectly()
    {
        // Arrange
        await _store.InitializeAsync(TestCollection, VectorDimension);
        
        var segmentTypes = new[] { SegmentType.Sentence, SegmentType.ListItem, SegmentType.Heading, SegmentType.CodeBlock };
        var segments = segmentTypes.Select((type, i) => new Segment($"type-test-{i}", $"Content {type}", type, i, 0, 50)
        {
            Embedding = CreateTestEmbedding(VectorDimension, seed: i)
        }).ToArray();

        // Act
        await _store.UpsertSegmentsAsync(TestCollection, segments);

        // Assert - verify each can be retrieved
        foreach (var (type, i) in segmentTypes.Select((t, i) => (t, i)))
        {
            var retrieved = await _store.GetDocumentSegmentsAsync(TestCollection, $"type_test_{i}");
            Assert.Single(retrieved);
            Assert.Equal(type, retrieved[0].Type);
        }
    }

    #endregion

    #region Helpers

    private static float[] CreateTestEmbedding(int dimension, int seed = 0)
    {
        var random = new Random(seed);
        var embedding = new float[dimension];
        
        for (int i = 0; i < dimension; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Range -1 to 1
        }
        
        // Normalize to unit length
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < dimension; i++)
            {
                embedding[i] /= magnitude;
            }
        }
        
        return embedding;
    }

    #endregion
}
