using Xunit;
using Xunit.Sdk;
using System.Reflection;
using SymbolicTesting.Core;

namespace SymbolicTesting.XUnit;

/// <summary>
/// Attribute to mark a method for symbolic testing
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SymbolicTestAttribute : Attribute
{
    /// <summary>
    /// Maximum number of paths to explore
    /// </summary>
    public int MaxPaths { get; set; } = 50;

    /// <summary>
    /// Maximum execution depth
    /// </summary>
    public int MaxDepth { get; set; } = 500;

    /// <summary>
    /// Timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Path exploration strategy
    /// </summary>
    public PathExplorationStrategy Strategy { get; set; } = PathExplorationStrategy.DepthFirst;
}

/// <summary>
/// Data attribute that generates test data using symbolic execution
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SymbolicDataAttribute : DataAttribute
{
    private readonly SymbolicTestAttribute _options;

    public SymbolicDataAttribute(
        int maxPaths = 50,
        int maxDepth = 500,
        int timeoutSeconds = 30)
    {
        _options = new SymbolicTestAttribute
        {
            MaxPaths = maxPaths,
            MaxDepth = maxDepth,
            TimeoutSeconds = timeoutSeconds
        };
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        // Find the target method to test
        var targetMethod = FindTargetMethod(testMethod);

        if (targetMethod == null)
        {
            yield return Array.Empty<object>();
            yield break;
        }

        // Run symbolic execution
        var executor = new SymbolicExecutor();
        var options = new ExplorationOptions
        {
            MaxPaths = _options.MaxPaths,
            MaxDepth = _options.MaxDepth,
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
            Strategy = _options.Strategy
        };

        var result = executor.ExploreAsync(targetMethod, options).GetAwaiter().GetResult();

        // Convert test cases to xUnit theory data
        foreach (var testCase in result.TestCases)
        {
            var parameters = targetMethod.GetParameters();
            var args = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramName = parameters[i].Name ?? $"arg{i}";
                if (testCase.Inputs.TryGetValue(paramName, out var value))
                {
                    args[i] = value;
                }
            }

            yield return args!;
        }
    }

    private MethodInfo? FindTargetMethod(MethodInfo testMethod)
    {
        // Look for a method with the same name minus "Test" prefix in the same class
        var testName = testMethod.Name;
        var targetName = testName.StartsWith("Test") ? testName.Substring(4) : testName;

        var targetMethod = testMethod.DeclaringType?
            .GetMethod(targetName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        return targetMethod;
    }
}

/// <summary>
/// Theory attribute that uses symbolic execution to generate test data
/// </summary>
[XunitTestCaseDiscoverer("SymbolicTesting.XUnit.SymbolicTheoryDiscoverer", "SymbolicTesting.XUnit")]
public class SymbolicTheoryAttribute : TheoryAttribute
{
    public int MaxPaths { get; set; } = 50;
    public int MaxDepth { get; set; } = 500;
    public int TimeoutSeconds { get; set; } = 30;
}
