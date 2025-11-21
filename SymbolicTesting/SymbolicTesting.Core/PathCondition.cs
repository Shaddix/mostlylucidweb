using System.Collections.Immutable;

namespace SymbolicTesting.Core;

/// <summary>
/// Represents a constraint on the execution path
/// </summary>
public record PathConstraint
{
    public required SymbolicValue Condition { get; init; }
    public required bool IsTrueBranch { get; init; }

    public override string ToString() => IsTrueBranch ? $"{Condition}" : $"!({Condition})";
}

/// <summary>
/// Represents the accumulated constraints along an execution path
/// </summary>
public class PathCondition
{
    private readonly ImmutableList<PathConstraint> _constraints;

    public PathCondition()
    {
        _constraints = ImmutableList<PathConstraint>.Empty;
    }

    private PathCondition(ImmutableList<PathConstraint> constraints)
    {
        _constraints = constraints;
    }

    public IReadOnlyList<PathConstraint> Constraints => _constraints;

    /// <summary>
    /// Adds a new constraint to this path condition
    /// </summary>
    public PathCondition AddConstraint(SymbolicValue condition, bool isTrueBranch)
    {
        var constraint = new PathConstraint
        {
            Condition = condition,
            IsTrueBranch = isTrueBranch
        };
        return new PathCondition(_constraints.Add(constraint));
    }

    /// <summary>
    /// Checks if this path condition is satisfiable
    /// </summary>
    public bool IsSatisfiable()
    {
        // This will be implemented by the constraint solver
        // For now, assume all paths are satisfiable
        return true;
    }

    public override string ToString()
    {
        if (_constraints.Count == 0)
            return "true";

        return string.Join(" && ", _constraints);
    }
}
