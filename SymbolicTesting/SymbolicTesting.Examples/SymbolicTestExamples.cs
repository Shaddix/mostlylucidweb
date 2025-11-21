using Xunit;
using SymbolicTesting.XUnit;

namespace SymbolicTesting.Examples;

/// <summary>
/// Examples of using the SymbolicTest attribute
/// </summary>
public class SymbolicTestExamples
{
    /// <summary>
    /// Basic symbolic theory test - framework generates test data
    /// </summary>
    [Theory]
    [SymbolicData(maxPaths: 10)]
    public void TestClassify(int number)
    {
        var result = Calculator.Classify(number);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Test with multiple parameters
    /// </summary>
    [Theory]
    [SymbolicData(maxPaths: 20)]
    public void TestCompute(int x, int y)
    {
        var result = Calculator.Compute(x, y);
        // The symbolic executor will find inputs that exercise all paths
        Assert.True(result != 0 || x == 0 || y == 0);
    }

    /// <summary>
    /// Test boolean parameter
    /// </summary>
    [Theory]
    [SymbolicData(maxPaths: 10)]
    public void TestIsValid(int age, bool hasLicense)
    {
        var result = Calculator.IsValid(age, hasLicense);

        // Verify the logic
        if (age >= 18 && hasLicense)
            Assert.True(result);
        else
            Assert.False(result);
    }

    /// <summary>
    /// Manual test to show the difference
    /// </summary>
    [Fact]
    public void ManualTest_Classify_Negative()
    {
        var result = Calculator.Classify(-5);
        Assert.Equal("negative", result);
    }

    [Fact]
    public void ManualTest_Classify_Zero()
    {
        var result = Calculator.Classify(0);
        Assert.Equal("zero", result);
    }

    [Fact]
    public void ManualTest_Classify_SmallPositive()
    {
        var result = Calculator.Classify(5);
        Assert.Equal("small positive", result);
    }

    [Fact]
    public void ManualTest_Classify_LargePositive()
    {
        var result = Calculator.Classify(100);
        Assert.Equal("large positive", result);
    }
}

/// <summary>
/// Examples showing how symbolic execution finds edge cases
/// </summary>
public class EdgeCaseDiscovery
{
    /// <summary>
    /// Symbolic execution can find the divide-by-zero case
    /// </summary>
    [Theory]
    [SymbolicData(maxPaths: 5)]
    public void TestDivide_FindsEdgeCases(int numerator, int denominator)
    {
        // The framework should generate a test case with denominator=0
        // which will throw DivideByZeroException

        if (denominator == 0)
        {
            Assert.Throws<DivideByZeroException>(() => Calculator.Divide(numerator, denominator));
        }
        else
        {
            var result = Calculator.Divide(numerator, denominator);
            Assert.Equal(numerator / denominator, result);
        }
    }
}
