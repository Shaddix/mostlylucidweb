using Mostlylucid.DocSummarizer.Config;
using Xunit;

namespace Mostlylucid.DocSummarizer.Tests.Config;

/// <summary>
/// Tests for EmbeddingBackend enum and OnnxConfig
/// </summary>
public class EmbeddingBackendConfigTests
{
    [Fact]
    public void EmbeddingBackend_Onnx_HasValue0()
    {
        // Assert
        Assert.Equal(0, (int)EmbeddingBackend.Onnx);
    }

    [Fact]
    public void EmbeddingBackend_Ollama_HasValue1()
    {
        // Assert
        Assert.Equal(1, (int)EmbeddingBackend.Ollama);
    }

    [Fact]
    public void OnnxConfig_DefaultModel_IsBgeBaseEnV15()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert - BGE-base is the default (better quality than MiniLM)
        Assert.Equal(OnnxEmbeddingModel.BgeBaseEnV15, config.EmbeddingModel);
    }

    [Fact]
    public void OnnxConfig_DefaultUseQuantized_IsTrue()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert
        Assert.True(config.UseQuantized);
    }

    [Fact]
    public void OnnxConfig_DefaultMaxSequenceLength_Is512()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert - 512 is the default for BGE models
        Assert.Equal(512, config.MaxEmbeddingSequenceLength);
    }

    [Fact]
    public void OnnxConfig_DefaultInferenceThreads_Is0()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert - 0 means auto
        Assert.Equal(0, config.InferenceThreads);
    }

    [Fact]
    public void OnnxConfig_ModelDirectory_IsInAppDirectory()
    {
        // Arrange & Act
        var config = new OnnxConfig();
        var appDir = AppContext.BaseDirectory;

        // Assert - model directory should be [app dir]/models so models travel with the tool
        Assert.StartsWith(appDir, config.ModelDirectory);
        Assert.Contains("models", config.ModelDirectory);
    }

    [Theory]
    [InlineData(OnnxEmbeddingModel.AllMiniLmL6V2)]
    [InlineData(OnnxEmbeddingModel.BgeSmallEnV15)]
    [InlineData(OnnxEmbeddingModel.BgeBaseEnV15)]
    [InlineData(OnnxEmbeddingModel.BgeLargeEnV15)]
    [InlineData(OnnxEmbeddingModel.GteSmall)]
    [InlineData(OnnxEmbeddingModel.GteBase)]
    [InlineData(OnnxEmbeddingModel.GteLarge)]
    [InlineData(OnnxEmbeddingModel.MultiQaMiniLm)]
    [InlineData(OnnxEmbeddingModel.ParaphraseMiniLmL3)]
    [InlineData(OnnxEmbeddingModel.JinaEmbeddingsV2BaseEn)]
    [InlineData(OnnxEmbeddingModel.SnowflakeArcticEmbedM)]
    [InlineData(OnnxEmbeddingModel.NomicEmbedTextV15)]
    public void OnnxConfig_EmbeddingModel_CanBeSet(OnnxEmbeddingModel expectedModel)
    {
        // Arrange
        var config = new OnnxConfig();

        // Act
        config.EmbeddingModel = expectedModel;

        // Assert
        Assert.Equal(expectedModel, config.EmbeddingModel);
    }

    [Fact]
    public void DocSummarizerConfig_DefaultEmbeddingBackend_IsOnnx()
    {
        // Arrange & Act
        var config = new DocSummarizerConfig();

        // Assert
        Assert.Equal(EmbeddingBackend.Onnx, config.EmbeddingBackend);
    }

    [Fact]
    public void DocSummarizerConfig_OnnxConfig_IsNotNull()
    {
        // Arrange & Act
        var config = new DocSummarizerConfig();

        // Assert
        Assert.NotNull(config.Onnx);
    }

    [Theory]
    [InlineData(EmbeddingBackend.Onnx)]
    [InlineData(EmbeddingBackend.Ollama)]
    public void DocSummarizerConfig_EmbeddingBackend_CanBeSet(EmbeddingBackend backend)
    {
        // Arrange
        var config = new DocSummarizerConfig();

        // Act
        config.EmbeddingBackend = backend;

        // Assert
        Assert.Equal(backend, config.EmbeddingBackend);
    }
}
