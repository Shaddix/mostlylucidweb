using Mostlylucid.DocSummarizer.Services.Utilities;
using Xunit;

namespace Mostlylucid.DocSummarizer.Core.Tests.Services;

/// <summary>
/// Tests for VectorMath utility functions
/// </summary>
public class VectorMathTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void CosineSimilarity_IdenticalVectors_Returns1()
    {
        // Arrange
        var a = new float[] { 1, 2, 3, 4, 5 };
        var b = new float[] { 1, 2, 3, 4, 5 };

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(1.0, similarity, 5);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsMinus1()
    {
        // Arrange
        var a = new float[] { 1, 2, 3 };
        var b = new float[] { -1, -2, -3 };

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(-1.0, similarity, 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_Returns0()
    {
        // Arrange
        var a = new float[] { 1, 0, 0 };
        var b = new float[] { 0, 1, 0 };

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(0.0, similarity, 5);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_Returns0()
    {
        // Arrange
        var a = new float[] { 1, 2, 3 };
        var b = new float[] { 1, 2 };

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_Returns0()
    {
        // Arrange
        var a = Array.Empty<float>();
        var b = Array.Empty<float>();

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_Returns0()
    {
        // Arrange
        var a = new float[] { 0, 0, 0 };
        var b = new float[] { 1, 2, 3 };

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(0.0, similarity);
    }

    [Fact]
    public void CosineSimilarity_LargeVectors_WorksWithSimd()
    {
        // Arrange - 384 dimensions like embedding vectors
        var a = new float[384];
        var b = new float[384];
        for (int i = 0; i < 384; i++)
        {
            a[i] = i * 0.1f;
            b[i] = i * 0.1f;
        }

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(1.0, similarity, 5);
    }

    [Fact]
    public void EuclideanDistance_SameVectors_Returns0()
    {
        // Arrange
        var a = new float[] { 1, 2, 3 };
        var b = new float[] { 1, 2, 3 };

        // Act
        var distance = VectorMath.EuclideanDistance(a, b);

        // Assert
        Assert.Equal(0.0, distance, 5);
    }

    [Fact]
    public void EuclideanDistance_DifferentVectors_ReturnsPositive()
    {
        // Arrange
        var a = new float[] { 0, 0, 0 };
        var b = new float[] { 3, 4, 0 };

        // Act
        var distance = VectorMath.EuclideanDistance(a, b);

        // Assert
        Assert.Equal(5.0, distance, 5); // 3-4-5 triangle
    }

    [Fact]
    public void DotProduct_SimpleVectors_ReturnsCorrectValue()
    {
        // Arrange
        var a = new float[] { 1, 2, 3 };
        var b = new float[] { 4, 5, 6 };

        // Act
        var result = VectorMath.DotProduct(a, b);

        // Assert
        Assert.Equal(32.0f, result); // 1*4 + 2*5 + 3*6 = 32
    }

    [Fact]
    public void L2Norm_SimpleVector_ReturnsCorrectValue()
    {
        // Arrange
        var v = new float[] { 3, 4 };

        // Act
        var norm = VectorMath.L2Norm(v);

        // Assert
        Assert.Equal(5.0f, norm); // sqrt(9 + 16) = 5
    }

    [Fact]
    public void NormalizeInPlace_NonZeroVector_ResultHasUnitLength()
    {
        // Arrange
        var v = new float[] { 3, 4 };

        // Act
        VectorMath.NormalizeInPlace(v);
        var norm = VectorMath.L2Norm(v);

        // Assert
        Assert.Equal(1.0f, norm, 5);
    }

    [Fact]
    public void Normalize_ReturnsNewArray_DoesNotModifyOriginal()
    {
        // Arrange
        var original = new float[] { 3, 4 };
        var originalCopy = original.ToArray();

        // Act
        var normalized = VectorMath.Normalize(original);

        // Assert
        Assert.Equal(originalCopy, original);
        Assert.NotSame(original, normalized);
        Assert.Equal(1.0f, VectorMath.L2Norm(normalized), 5);
    }

    [Fact]
    public void ComputeCentroid_MultipleVectors_ReturnsAverage()
    {
        // Arrange
        var vectors = new[]
        {
            new float[] { 2, 4, 6 },
            new float[] { 4, 6, 8 },
            new float[] { 6, 8, 10 }
        };

        // Act
        var centroid = VectorMath.ComputeCentroid(vectors);

        // Assert
        Assert.Equal(new float[] { 4, 6, 8 }, centroid);
    }

    [Fact]
    public void ComputeCentroid_EmptyList_ReturnsEmptyArray()
    {
        // Arrange
        var vectors = Array.Empty<float[]>();

        // Act
        var centroid = VectorMath.ComputeCentroid(vectors);

        // Assert
        Assert.Empty(centroid);
    }

    [Fact]
    public void WeightedAverage_EqualWeights_SameAsCentroid()
    {
        // Arrange
        var vectors = new[]
        {
            (new float[] { 2, 4 }, 1.0),
            (new float[] { 4, 6 }, 1.0)
        };

        // Act
        var result = VectorMath.WeightedAverage(vectors);

        // Assert
        Assert.Equal(new float[] { 3, 5 }, result);
    }

    [Fact]
    public void WeightedAverage_DifferentWeights_WeightsCorrectly()
    {
        // Arrange
        var vectors = new[]
        {
            (new float[] { 0, 0 }, 1.0),
            (new float[] { 10, 10 }, 3.0)
        };

        // Act
        var result = VectorMath.WeightedAverage(vectors);

        // Assert - should be (10*3 + 0*1) / 4 = 7.5
        Assert.Equal(7.5f, result[0], 3);
        Assert.Equal(7.5f, result[1], 3);
    }

    [Fact]
    public void TopKBySimilarity_ReturnsTopK()
    {
        // Arrange
        var query = new float[] { 1, 0, 0 };
        var vectors = new[]
        {
            new float[] { 1, 0, 0 },   // Most similar
            new float[] { 0, 1, 0 },   // Orthogonal
            new float[] { 0.5f, 0.5f, 0 }, // Partially similar
            new float[] { -1, 0, 0 }   // Opposite
        };

        // Act
        var result = VectorMath.TopKBySimilarity(query, vectors, 2);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].Index); // Most similar first
        Assert.Equal(2, result[1].Index); // Second most similar
    }

    [Fact]
    public void CosineSimilarity_DoubleVersion_Works()
    {
        // Arrange
        var a = new double[] { 1, 2, 3 };
        var b = new double[] { 1, 2, 3 };

        // Act
        var similarity = VectorMath.CosineSimilarity(a, b);

        // Assert
        Assert.Equal(1.0, similarity, 5);
    }
}
