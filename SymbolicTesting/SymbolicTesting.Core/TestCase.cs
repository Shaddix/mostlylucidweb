namespace SymbolicTesting.Core;

/// <summary>
/// Represents a generated test case with concrete input values
/// </summary>
public class TestCase
{
    public required string MethodName { get; init; }
    public required Dictionary<string, object?> Inputs { get; init; }
    public required PathCondition PathCondition { get; init; }
    public object? ExpectedResult { get; init; }
    public Type? ExpectedException { get; init; }
    public string? Description { get; init; }

    public override string ToString()
    {
        var inputs = string.Join(", ", Inputs.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"TestCase({MethodName}, {inputs})";
    }
}

/// <summary>
/// Result of symbolic execution exploration
/// </summary>
public class ExplorationResult
{
    public required List<TestCase> TestCases { get; init; }
    public required int PathsExplored { get; init; }
    public required int PathsSatisfiable { get; init; }
    public required TimeSpan ExecutionTime { get; init; }
    public List<string> Warnings { get; init; } = new();

    public override string ToString()
    {
        return $"Explored {PathsExplored} paths, generated {TestCases.Count} test cases in {ExecutionTime.TotalSeconds:F2}s";
    }
}
