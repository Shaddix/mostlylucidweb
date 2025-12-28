using Mostlylucid.DocSummarizer.Config;
using Xunit;

namespace Mostlylucid.DocSummarizer.Core.Tests.Config;

/// <summary>
/// Tests for EmbeddingBackend enum
/// </summary>
public class EmbeddingBackendTests
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
    public void OnnxExecutionProvider_Cpu_HasValue0()
    {
        // Assert
        Assert.Equal(0, (int)OnnxExecutionProvider.Cpu);
    }

    [Fact]
    public void OnnxExecutionProvider_Cuda_HasValue1()
    {
        // Assert
        Assert.Equal(1, (int)OnnxExecutionProvider.Cuda);
    }

    [Fact]
    public void OnnxExecutionProvider_DirectMl_HasValue2()
    {
        // Assert
        Assert.Equal(2, (int)OnnxExecutionProvider.DirectMl);
    }

    [Fact]
    public void OnnxExecutionProvider_Auto_HasValue3()
    {
        // Assert
        Assert.Equal(3, (int)OnnxExecutionProvider.Auto);
    }
}
