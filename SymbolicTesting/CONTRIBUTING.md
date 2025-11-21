# Contributing to Symbolic Testing Framework

Thank you for your interest in contributing! This document provides guidelines for contributing to the project.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/yourusername/SymbolicTesting.git`
3. Create a feature branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Run tests: `dotnet test`
6. Commit your changes: `git commit -m "Add your feature"`
7. Push to your fork: `git push origin feature/your-feature-name`
8. Open a Pull Request

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022, VS Code, or Rider
- Git

### Building

```bash
dotnet build SymbolicTesting.sln
```

### Running Tests

```bash
dotnet test SymbolicTesting.Tests
dotnet test SymbolicTesting.Examples
```

## Code Style

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and small
- Use nullable reference types (`#nullable enable`)

Example:

```csharp
/// <summary>
/// Executes a basic block symbolically
/// </summary>
/// <param name="state">Current execution state</param>
/// <param name="block">Basic block to execute</param>
/// <returns>Result of block execution</returns>
private BlockExecutionResult ExecuteBlock(ExecutionState state, BasicBlock block)
{
    // Implementation
}
```

## Project Structure

```
SymbolicTesting/
├── SymbolicTesting.Core/           # Core abstractions
├── SymbolicTesting.Analysis/       # IL analysis
├── SymbolicTesting.Constraints/    # Constraint solving
├── SymbolicTesting.TestGeneration/ # Test code generation
├── SymbolicTesting.XUnit/          # xUnit integration
├── SymbolicTesting.Cli/            # CLI tool
├── SymbolicTesting.Examples/       # Example usage
└── SymbolicTesting.Tests/          # Unit tests
```

## Areas for Contribution

### High Priority

1. **More IL Instructions**: Implement support for additional IL opcodes
2. **Loop Handling**: Add bounded loop unrolling
3. **Object Support**: Improve heap modeling
4. **Performance**: Optimize path exploration and constraint solving
5. **Documentation**: Improve docs and add tutorials

### Medium Priority

6. **String Operations**: Add symbolic string support
7. **Floating Point**: Add float/double support
8. **Array Support**: Handle arrays symbolically
9. **Exception Handling**: Model try/catch blocks
10. **Coverage Tracking**: Measure actual code coverage

### Advanced

11. **Concolic Execution**: Hybrid concrete/symbolic execution
12. **Path Merging**: Reduce path explosion
13. **Parallel Exploration**: Multi-threaded path exploration
14. **Visual Studio Integration**: Create VS extension

## Adding New IL Instructions

To add support for a new IL instruction:

1. Open `SymbolicTesting.Core/SymbolicExecutor.cs`
2. Find the `ExecuteInstruction` method
3. Add a new case for your instruction:

```csharp
case Code.YourInstruction =>
{
    // 1. Pop operands from stack if needed
    var (state1, operand) = state.Pop();

    // 2. Create symbolic result
    var result = new SymbolicExpression
    {
        Operator = SymbolicOperator.YourOperation,
        Operands = new[] { operand },
        Type = typeof(ResultType)
    };

    // 3. Push result and return new state
    return state1.Push(result);
}
```

4. Add tests in `SymbolicTesting.Tests`
5. Update documentation

## Writing Tests

Use xUnit for unit tests:

```csharp
public class SymbolicExecutorTests
{
    [Fact]
    public async Task ExecuteSimpleMethod_GeneratesTestCases()
    {
        // Arrange
        var method = typeof(Calculator).GetMethod("Add");
        var executor = new SymbolicExecutor();
        var options = new ExplorationOptions { MaxPaths = 10 };

        // Act
        var result = await executor.ExploreAsync(method, options);

        // Assert
        Assert.NotEmpty(result.TestCases);
        Assert.True(result.PathsExplored > 0);
    }
}
```

## Pull Request Guidelines

### Before Submitting

- [ ] Code builds without errors
- [ ] All tests pass
- [ ] New code has tests
- [ ] Public APIs have XML documentation
- [ ] README updated if needed
- [ ] No unnecessary dependencies added

### PR Description Template

```markdown
## Description
Brief description of your changes

## Motivation
Why is this change needed?

## Changes
- List of changes made
- Each change on a new line

## Testing
How did you test these changes?

## Breaking Changes
List any breaking changes (if applicable)

## Related Issues
Closes #123 (if applicable)
```

## Reporting Bugs

When reporting bugs, please include:

1. **Description**: Clear description of the bug
2. **Steps to Reproduce**: Exact steps to reproduce the issue
3. **Expected Behavior**: What you expected to happen
4. **Actual Behavior**: What actually happened
5. **Code Sample**: Minimal code that reproduces the issue
6. **Environment**: OS, .NET version, etc.

Example:

```markdown
### Bug: Symbolic execution fails on nested loops

**Description**: The framework crashes when analyzing methods with nested loops.

**Steps to Reproduce**:
1. Create method with nested for loops
2. Run symbolic execution
3. Observe crash

**Code Sample**:
\`\`\`csharp
public static int NestedLoops(int n)
{
    for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
            if (i == j) return i;
    return -1;
}
\`\`\`

**Expected**: Should explore paths with loop bounds
**Actual**: Throws StackOverflowException

**Environment**: Windows 11, .NET 9.0
```

## Feature Requests

For feature requests:

1. Check if the feature already exists
2. Check if there's an open issue for it
3. If not, open a new issue with:
   - Clear description of the feature
   - Use cases
   - Example code showing desired usage
   - Why this would be beneficial

## Code Review Process

1. All submissions require review
2. Reviewers will check:
   - Code quality and style
   - Test coverage
   - Documentation
   - Performance implications
3. Address review feedback
4. Once approved, maintainers will merge

## Questions?

- Open an issue for questions
- Check existing issues first
- Be respectful and follow code of conduct

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing! 🎉
