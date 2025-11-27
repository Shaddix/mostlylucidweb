# Symbolic Testing Framework for .NET

A modern symbolic execution testing framework for .NET 9.0+, inspired by Microsoft's PEX/IntelliTest. This framework automatically generates test cases by exploring execution paths and using constraint solving to find inputs that maximize code coverage.

## Overview

Symbolic execution is a powerful program analysis technique that:
- Executes code with **symbolic values** instead of concrete inputs
- Explores **multiple execution paths** simultaneously
- Uses **constraint solvers** (Z3) to generate concrete test inputs
- Automatically finds **edge cases** and **boundary conditions**
- Maximizes **code coverage** with minimal manual test writing

### Key Features

- **Automatic Test Generation**: Generate comprehensive test suites automatically
- **Path Exploration**: Systematically explore all execution paths in your code
- **Constraint Solving**: Use Z3 SMT solver to find concrete input values
- **xUnit Integration**: Seamless integration with xUnit test framework
- **IL Analysis**: Analyze compiled .NET assemblies using Mono.Cecil
- **Modern .NET**: Built for .NET 9.0 with nullable reference types
- **Extensible**: Modular architecture for easy customization

## Architecture

The framework consists of several layers:

```
┌─────────────────────────────────────┐
│   xUnit Integration & CLI Tool      │
├─────────────────────────────────────┤
│      Test Code Generation           │
├─────────────────────────────────────┤
│   Symbolic Execution Engine         │
├─────────────────────────────────────┤
│  IL Analysis    │  Constraint Solver│
│  (Mono.Cecil)   │      (Z3)         │
├─────────────────────────────────────┤
│         Core Abstractions           │
└─────────────────────────────────────┘
```

### Components

1. **SymbolicTesting.Core**: Core abstractions (symbolic values, execution state, path conditions)
2. **SymbolicTesting.Analysis**: IL code analysis using Mono.Cecil
3. **SymbolicTesting.Constraints**: Z3 integration for constraint solving
4. **SymbolicTesting.TestGeneration**: Roslyn-based test code generation
5. **SymbolicTesting.XUnit**: xUnit test framework integration
6. **SymbolicTesting.Cli**: Command-line tool for batch test generation

## Quick Start

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd SymbolicTesting

# Build the solution (requires .NET 9.0 SDK)
dotnet build
```

### Usage: xUnit Attributes

The easiest way to use symbolic testing is with xUnit attributes:

```csharp
using SymbolicTesting.XUnit;
using Xunit;

public class Calculator
{
    public static string Classify(int number)
    {
        if (number < 0)
            return "negative";
        else if (number == 0)
            return "zero";
        else if (number < 10)
            return "small positive";
        else
            return "large positive";
    }
}

public class CalculatorTests
{
    [Theory]
    [SymbolicData(maxPaths: 10)]
    public void TestClassify(int number)
    {
        var result = Calculator.Classify(number);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
```

The `[SymbolicData]` attribute automatically:
1. Analyzes the `Classify` method
2. Explores up to 10 execution paths
3. Generates test inputs that cover all branches
4. Runs your test with each generated input

### Usage: CLI Tool

Generate tests from the command line:

```bash
dotnet run --project SymbolicTesting.Cli -- \
    --assembly MyApp.dll \
    --type MyNamespace.Calculator \
    --method Classify \
    --output GeneratedTests.cs \
    --max-paths 100 \
    --timeout 30
```

This generates a complete xUnit test class:

```csharp
namespace MyNamespace;

public class Calculator_Classify_GeneratedTests
{
    [Fact]
    public void Test_Classify_0()
    {
        var result = Calculator.Classify(-1);
        Assert.NotNull(result);
    }

    [Fact]
    public void Test_Classify_1()
    {
        var result = Calculator.Classify(0);
        Assert.NotNull(result);
    }

    [Fact]
    public void Test_Classify_2()
    {
        var result = Calculator.Classify(5);
        Assert.NotNull(result);
    }

    [Fact]
    public void Test_Classify_3()
    {
        var result = Calculator.Classify(100);
        Assert.NotNull(result);
    }
}
```

### Usage: Programmatic API

Use the API directly in your code:

```csharp
using SymbolicTesting.Core;
using System.Reflection;

// Get the method to analyze
var method = typeof(Calculator).GetMethod("Classify");

// Configure exploration options
var options = new ExplorationOptions
{
    MaxPaths = 100,
    MaxDepth = 1000,
    Timeout = TimeSpan.FromSeconds(30),
    Strategy = PathExplorationStrategy.DepthFirst
};

// Run symbolic execution
var executor = new SymbolicExecutor();
var result = await executor.ExploreAsync(method, options);

// Examine results
Console.WriteLine($"Explored {result.PathsExplored} paths");
Console.WriteLine($"Generated {result.TestCases.Count} test cases");

foreach (var testCase in result.TestCases)
{
    Console.WriteLine($"Inputs: {string.Join(", ", testCase.Inputs)}");
}
```

## Examples

See the `SymbolicTesting.Examples` project for complete examples:

### Example 1: Simple Branching

```csharp
public static string Classify(int number)
{
    if (number < 0)
        return "negative";
    else if (number == 0)
        return "zero";
    else if (number < 10)
        return "small positive";
    else
        return "large positive";
}
```

Symbolic execution generates inputs: `-1, 0, 5, 10` covering all paths.

### Example 2: Finding Edge Cases

```csharp
public static int Divide(int numerator, int denominator)
{
    if (denominator == 0)
        throw new DivideByZeroException();
    return numerator / denominator;
}
```

Symbolic execution automatically finds the divide-by-zero case!

### Example 3: Complex Conditions

```csharp
public static bool IsValid(int age, bool hasLicense)
{
    if (age >= 18)
    {
        if (hasLicense)
            return true;
        else
            return false;
    }
    else
    {
        return false;
    }
}
```

Generates test cases covering all combinations of age and license status.

## How It Works

### 1. IL Analysis

The framework uses Mono.Cecil to:
- Parse compiled .NET assemblies
- Extract IL (Intermediate Language) instructions
- Build control flow graphs
- Identify branch points

### 2. Symbolic Execution

The engine executes code symbolically:
- Variables hold symbolic values instead of concrete values
- Operations create symbolic expressions (e.g., `x + 5` becomes `SymbolicExpression(Add, [x, 5])`)
- Branch conditions accumulate path constraints

### 3. Path Exploration

Different strategies explore paths:
- **Depth-First**: Explore deeply before backtracking
- **Breadth-First**: Explore all paths at same depth
- **Random**: Randomly select paths
- **Coverage**: Prioritize paths that cover new code

### 4. Constraint Solving

At each path completion:
- Path constraints are sent to Z3 SMT solver
- Z3 finds concrete values satisfying all constraints
- If satisfiable, generate a test case
- If unsatisfiable, the path is unreachable

### 5. Test Generation

Generated test cases are converted to:
- xUnit test methods using Roslyn
- Can be run immediately or saved to files

## Comparison with PEX/IntelliTest

| Feature | PEX/IntelliTest | This Framework |
|---------|-----------------|----------------|
| .NET Version | .NET Framework | .NET 9.0+ |
| Visual Studio Integration | Yes | No (CLI/xUnit) |
| IL Analysis | Yes | Yes (Mono.Cecil) |
| Constraint Solver | Z3 | Z3 |
| Open Source | No | Yes |
| Extensible | Limited | Yes |
| Test Framework | MSTest | xUnit |
| Modern C# | No | Yes (Records, etc.) |

## Configuration

### Exploration Options

```csharp
var options = new ExplorationOptions
{
    MaxPaths = 100,          // Maximum paths to explore
    MaxDepth = 1000,         // Maximum instruction depth
    Timeout = TimeSpan.FromSeconds(30),
    Strategy = PathExplorationStrategy.DepthFirst,
    MinimizeTestCases = true,
    RandomSeed = 42          // For reproducibility
};
```

### Attribute Options

```csharp
[SymbolicData(
    maxPaths: 50,
    maxDepth: 500,
    timeoutSeconds: 30
)]
```

## Limitations

Current limitations (opportunities for contribution!):

1. **Limited IL Coverage**: Not all IL instructions are implemented
2. **No Loop Handling**: Loops may cause infinite exploration
3. **No Object Creation**: Limited support for complex objects
4. **No External Calls**: Cannot symbolically execute external methods
5. **Integer Types Only**: Limited type support (no floating point yet)
6. **Simple Constraints**: Complex mathematical constraints may timeout

## Development

### Building

```bash
dotnet build SymbolicTesting.sln
```

### Running Tests

```bash
dotnet test SymbolicTesting.Tests
```

### Running Examples

```bash
dotnet test SymbolicTesting.Examples
```

## Contributing

Contributions welcome! Areas for improvement:

1. **More IL Instructions**: Implement more opcodes
2. **Loop Detection**: Handle loops intelligently
3. **Object Support**: Better handling of object creation
4. **Performance**: Optimize path exploration
5. **Type Support**: Add more types (float, double, string operations)
6. **Concolic Execution**: Hybrid concrete/symbolic execution
7. **Code Coverage Tracking**: Measure actual coverage achieved

## License

MIT License - See LICENSE file

## Acknowledgments

- Inspired by Microsoft's PEX/IntelliTest
- Uses Microsoft Z3 for constraint solving
- Uses Mono.Cecil for IL analysis
- Built on xUnit test framework

## Resources

- [PEX: White-box Test Generation](https://www.microsoft.com/en-us/research/project/pex-white-box-test-generation-for-net/)
- [Symbolic Execution for Software Testing](https://dl.acm.org/doi/10.1145/2408776.2408795)
- [Z3 Theorem Prover](https://github.com/Z3Prover/z3)
- [Mono.Cecil Documentation](https://github.com/jbevain/cecil)

## Contact

For questions, issues, or contributions, please open an issue on GitHub.
