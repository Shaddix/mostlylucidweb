# Quick Start Guide

Get started with Symbolic Testing Framework in 5 minutes!

## Installation

### 1. Clone or Download

```bash
git clone <repository-url>
cd SymbolicTesting
```

### 2. Build

```bash
dotnet build
```

That's it! The framework is ready to use.

## Your First Symbolic Test

### Step 1: Create a Class to Test

Create a new file `MyMath.cs`:

```csharp
public class MyMath
{
    public static string Classify(int number)
    {
        if (number < 0)
            return "negative";
        else if (number == 0)
            return "zero";
        else
            return "positive";
    }
}
```

This simple method has 3 branches. Writing manual tests would require at least 3 test methods.

### Step 2: Create a Symbolic Test

Create `MyMathTests.cs`:

```csharp
using SymbolicTesting.XUnit;
using Xunit;

public class MyMathTests
{
    [Theory]
    [SymbolicData(maxPaths: 10)]
    public void TestClassify(int number)
    {
        // The framework will automatically generate inputs
        var result = MyMath.Classify(number);

        // Just verify the result is valid
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
```

### Step 3: Run the Test

```bash
dotnet test
```

**What happened?**

The framework:
1. ✅ Analyzed `Classify` method
2. ✅ Found 3 execution paths (negative, zero, positive)
3. ✅ Generated 3 test inputs: `-1, 0, 1`
4. ✅ Ran your test 3 times with different inputs
5. ✅ Achieved 100% code coverage!

## Example 2: Finding Edge Cases

The framework can find edge cases automatically:

```csharp
public class SafeDivide
{
    public static int Divide(int a, int b)
    {
        if (b == 0)
            throw new DivideByZeroException();
        return a / b;
    }
}

public class SafeDivideTests
{
    [Theory]
    [SymbolicData(maxPaths: 5)]
    public void TestDivide(int a, int b)
    {
        if (b == 0)
        {
            // Framework found the edge case!
            Assert.Throws<DivideByZeroException>(() => SafeDivide.Divide(a, b));
        }
        else
        {
            var result = SafeDivide.Divide(a, b);
            Assert.Equal(a / b, result);
        }
    }
}
```

The framework automatically discovers that `b == 0` is a special case!

## Example 3: Multiple Parameters

```csharp
public class RangeChecker
{
    public static bool IsInRange(int value, int min, int max)
    {
        if (min > max)
            throw new ArgumentException("min must be <= max");

        return value >= min && value <= max;
    }
}

public class RangeCheckerTests
{
    [Theory]
    [SymbolicData(maxPaths: 20)]
    public void TestIsInRange(int value, int min, int max)
    {
        if (min > max)
        {
            Assert.Throws<ArgumentException>(() =>
                RangeChecker.IsInRange(value, min, max));
        }
        else
        {
            var result = RangeChecker.IsInRange(value, min, max);

            // Verify the logic
            bool expected = value >= min && value <= max;
            Assert.Equal(expected, result);
        }
    }
}
```

The framework explores different combinations of value, min, and max!

## Using the CLI Tool

Generate test files for existing code:

```bash
cd SymbolicTesting.Cli

dotnet run -- \
    --assembly ../YourApp/bin/Debug/net9.0/YourApp.dll \
    --type YourNamespace.YourClass \
    --method YourMethod \
    --output GeneratedTests.cs \
    --max-paths 50
```

This creates a complete test file with all generated test cases!

## Configuration Options

### SymbolicData Attribute Options

```csharp
[SymbolicData(
    maxPaths: 50,        // How many paths to explore
    maxDepth: 500,       // Maximum instruction depth
    timeoutSeconds: 30   // Timeout for exploration
)]
```

### When to Use What

- **Simple methods (< 5 branches)**: `maxPaths: 10-20`
- **Medium methods (5-15 branches)**: `maxPaths: 50-100`
- **Complex methods (> 15 branches)**: `maxPaths: 100-200`, increase timeout

## Understanding the Output

When you run tests, you'll see output like:

```
Explored 8 paths
Generated 5 test cases
Execution time: 1.23s
```

- **Paths explored**: Number of different execution paths tried
- **Test cases**: Number of unique test inputs generated (some paths may be unreachable)
- **Execution time**: Time spent analyzing and generating tests

## Common Patterns

### Pattern 1: Verify No Exceptions

```csharp
[Theory]
[SymbolicData(maxPaths: 20)]
public void MethodShouldNotThrow(int x, int y)
{
    // Should complete without exceptions
    var result = MyClass.MyMethod(x, y);
    Assert.NotNull(result);
}
```

### Pattern 2: Verify Properties

```csharp
[Theory]
[SymbolicData(maxPaths: 20)]
public void SortedResultIsCorrect(int[] array)
{
    var sorted = MySort.Sort(array);

    // Verify it's actually sorted
    for (int i = 1; i < sorted.Length; i++)
        Assert.True(sorted[i] >= sorted[i-1]);
}
```

### Pattern 3: Find Bug Conditions

```csharp
[Theory]
[SymbolicData(maxPaths: 50)]
public void TestForBugs(int input)
{
    try
    {
        var result = BuggyMethod(input);
        // Verify invariants
        Assert.True(result >= 0, "Result should always be non-negative");
    }
    catch (Exception ex)
    {
        // If it throws, document what input caused it
        Assert.Fail($"Unexpected exception with input {input}: {ex.Message}");
    }
}
```

## Next Steps

1. **Try the Examples**: Run `dotnet test` in `SymbolicTesting.Examples`
2. **Read the Docs**: See `README.md` for detailed documentation
3. **Explore Architecture**: Check `ARCHITECTURE.md` to understand how it works
4. **Contribute**: See `CONTRIBUTING.md` to help improve the framework

## Troubleshooting

### "No test cases generated"

- The method might be too simple (no branches)
- Try increasing `maxPaths`
- Check that the method has a body (not abstract/extern)

### "Timeout exceeded"

- Method is too complex
- Increase `timeoutSeconds`
- Reduce `maxPaths` to explore fewer paths
- Method may have loops (not fully supported yet)

### "Type not found"

- Make sure the assembly is built
- Check the full namespace path
- Method must be public or internal

## Tips for Best Results

1. ✅ Start with small, simple methods
2. ✅ Use symbolic tests alongside manual tests
3. ✅ Increase maxPaths gradually
4. ✅ Use assertions to verify correctness
5. ✅ Document edge cases you discover

## Get Help

- Read `README.md` for comprehensive guide
- Check `ARCHITECTURE.md` for technical details
- Open an issue for questions or bugs

Happy testing! 🎉
