using System.Reflection;

namespace SymbolicTesting.Core;

/// <summary>
/// Interface for symbolic execution engines
/// </summary>
public interface ISymbolicExecutor
{
    /// <summary>
    /// Symbolically executes a method and generates test cases
    /// </summary>
    Task<ExplorationResult> ExploreAsync(
        MethodInfo method,
        ExplorationOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for controlling symbolic execution
/// </summary>
public class ExplorationOptions
{
    /// <summary>
    /// Maximum number of paths to explore
    /// </summary>
    public int MaxPaths { get; init; } = 100;

    /// <summary>
    /// Maximum depth of execution (instruction count)
    /// </summary>
    public int MaxDepth { get; init; } = 1000;

    /// <summary>
    /// Timeout for exploration
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Path exploration strategy
    /// </summary>
    public PathExplorationStrategy Strategy { get; init; } = PathExplorationStrategy.DepthFirst;

    /// <summary>
    /// Whether to try to minimize test cases
    /// </summary>
    public bool MinimizeTestCases { get; init; } = true;

    /// <summary>
    /// Random seed for deterministic exploration
    /// </summary>
    public int? RandomSeed { get; init; }
}

/// <summary>
/// Strategy for exploring execution paths
/// </summary>
public enum PathExplorationStrategy
{
    DepthFirst,
    BreadthFirst,
    Random,
    Coverage
}
