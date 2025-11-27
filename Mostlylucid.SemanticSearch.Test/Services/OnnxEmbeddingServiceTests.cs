using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Services;
using Xunit;

namespace Mostlylucid.SemanticSearch.Test.Services;

public class OnnxEmbeddingServiceTests : IDisposable
{
    private readonly Mock<ILogger<OnnxEmbeddingService>> _loggerMock;
    private readonly SemanticSearchConfig _config;
    private readonly OnnxEmbeddingService _service;

    public OnnxEmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<OnnxEmbeddingService>>();

        // Use test configuration with disabled model for unit tests
        _config = new SemanticSearchConfig
        {
            Enabled = false, // Disabled by default for unit tests without actual model
            VectorSize = 384
        };

        _service = new OnnxEmbeddingService(_loggerMock.Object, _config);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenDisabled_ReturnsZeroVector()
    {
        // Arrange
        var text = "test text";

        // Act
        var result = await _service.GenerateEmbeddingAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(_config.VectorSize);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsZeroVector()
    {
        // Arrange
        var text = "";

        // Act
        var result = await _service.GenerateEmbeddingAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(_config.VectorSize);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithNullText_ReturnsZeroVector()
    {
        // Arrange
        string? text = null;

        // Act
        var result = await _service.GenerateEmbeddingAsync(text!);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(_config.VectorSize);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithWhitespace_ReturnsZeroVector()
    {
        // Arrange
        var text = "   \t\n   ";

        // Act
        var result = await _service.GenerateEmbeddingAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(_config.VectorSize);
        result.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_MultipleCalls_HandlesConcurrency()
    {
        // Arrange
        var texts = new[] { "text1", "text2", "text3", "text4", "text5" };

        // Act
        var tasks = texts.Select(t => _service.GenerateEmbeddingAsync(t));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Should().HaveCount(_config.VectorSize);
        });
    }

    public void Dispose()
    {
        _service.Dispose();
    }
}

/// <summary>
/// Integration tests that require actual ONNX model files
/// These tests are skipped if model files are not present
/// </summary>
public class OnnxEmbeddingServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<OnnxEmbeddingService>> _loggerMock;
    private readonly SemanticSearchConfig _config;
    private readonly OnnxEmbeddingService? _service;
    private readonly bool _modelAvailable;

    public OnnxEmbeddingServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<OnnxEmbeddingService>>();

        // Try to find the model files
        var modelPath = FindModelPath();
        var vocabPath = FindVocabPath();

        _modelAvailable = File.Exists(modelPath) && File.Exists(vocabPath);

        _config = new SemanticSearchConfig
        {
            Enabled = _modelAvailable,
            EmbeddingModelPath = modelPath,
            VocabPath = vocabPath,
            VectorSize = 384
        };

        if (_modelAvailable)
        {
            _service = new OnnxEmbeddingService(_loggerMock.Object, _config);
        }
    }

    private string FindModelPath()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Mostlylucid", "models", "all-MiniLM-L6-v2.onnx"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Mostlylucid", "models", "all-MiniLM-L6-v2.onnx"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Mostlylucid", "models", "all-MiniLM-L6-v2.onnx"),
            "models/all-MiniLM-L6-v2.onnx"
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string FindVocabPath()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Mostlylucid", "models", "vocab.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Mostlylucid", "models", "vocab.txt"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Mostlylucid", "models", "vocab.txt"),
            "models/vocab.txt"
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithActualModel_GeneratesValidEmbedding()
    {
        if (!_modelAvailable)
        {
            // Skip test if model not available
            return;
        }

        // Arrange
        var text = "This is a test sentence for semantic search.";

        // Act
        var result = await _service!.GenerateEmbeddingAsync(text);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(_config.VectorSize);
        result.Should().Contain(v => v != 0f); // Should have at least some non-zero values

        // Check L2 normalization (magnitude should be ~1.0)
        var magnitude = Math.Sqrt(result.Sum(v => v * v));
        magnitude.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SimilarTexts_ProduceSimilarEmbeddings()
    {
        if (!_modelAvailable) return;

        // Arrange
        var text1 = "The cat sat on the mat";
        var text2 = "A feline rested on the carpet";
        var text3 = "The weather is sunny today";

        // Act
        var embedding1 = await _service!.GenerateEmbeddingAsync(text1);
        var embedding2 = await _service.GenerateEmbeddingAsync(text2);
        var embedding3 = await _service.GenerateEmbeddingAsync(text3);

        // Assert
        var similarity12 = CosineSimilarity(embedding1, embedding2);
        var similarity13 = CosineSimilarity(embedding1, embedding3);

        // Similar texts should have higher similarity
        similarity12.Should().BeGreaterThan(similarity13);
        similarity12.Should().BeGreaterThan(0.5f); // Reasonably similar
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_LongText_IsTruncatedCorrectly()
    {
        if (!_modelAvailable) return;

        // Arrange
        var longText = string.Join(" ", Enumerable.Repeat("This is a long sentence", 200));

        // Act
        var result = await _service!.GenerateEmbeddingAsync(longText);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(_config.VectorSize);
        result.Should().Contain(v => v != 0f);
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
        return dotProduct; // Already normalized, so just dot product
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}
