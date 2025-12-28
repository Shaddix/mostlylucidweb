using Mostlylucid.DocSummarizer.Config;
using Xunit;

namespace Mostlylucid.DocSummarizer.Core.Tests.Config;

/// <summary>
/// Tests for OnnxConfig defaults and model enum
/// </summary>
public class OnnxConfigTests
{
    [Fact]
    public void OnnxConfig_DefaultEmbeddingModel_IsAllMiniLmL6V2()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert
        Assert.Equal(OnnxEmbeddingModel.AllMiniLmL6V2, config.EmbeddingModel);
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
    public void OnnxConfig_DefaultMaxSequenceLength_Is256()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert
        Assert.Equal(256, config.MaxEmbeddingSequenceLength);
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
    public void OnnxConfig_DefaultExecutionProvider_IsCpu()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert - CPU is the safe default
        Assert.Equal(OnnxExecutionProvider.Cpu, config.ExecutionProvider);
    }

    [Fact]
    public void OnnxConfig_DefaultGpuDeviceId_Is0()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert
        Assert.Equal(0, config.GpuDeviceId);
    }

    [Fact]
    public void OnnxConfig_DefaultEmbeddingBatchSize_Is32()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert
        Assert.Equal(32, config.EmbeddingBatchSize);
    }

    [Fact]
    public void OnnxConfig_DefaultUseParallelExecution_IsTrue()
    {
        // Arrange & Act
        var config = new OnnxConfig();

        // Assert
        Assert.True(config.UseParallelExecution);
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
    [InlineData(OnnxEmbeddingModel.GteSmall)]
    [InlineData(OnnxEmbeddingModel.MultiQaMiniLm)]
    [InlineData(OnnxEmbeddingModel.ParaphraseMiniLmL3)]
    public void OnnxConfig_EmbeddingModel_CanBeSet(OnnxEmbeddingModel expectedModel)
    {
        // Arrange
        var config = new OnnxConfig();

        // Act
        config.EmbeddingModel = expectedModel;

        // Assert
        Assert.Equal(expectedModel, config.EmbeddingModel);
    }

    [Theory]
    [InlineData(OnnxExecutionProvider.Cpu)]
    [InlineData(OnnxExecutionProvider.Cuda)]
    [InlineData(OnnxExecutionProvider.DirectMl)]
    [InlineData(OnnxExecutionProvider.Auto)]
    public void OnnxConfig_ExecutionProvider_CanBeSet(OnnxExecutionProvider provider)
    {
        // Arrange
        var config = new OnnxConfig();

        // Act
        config.ExecutionProvider = provider;

        // Assert
        Assert.Equal(provider, config.ExecutionProvider);
    }
}
