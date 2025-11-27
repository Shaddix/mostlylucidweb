using System.Collections.Immutable;

namespace SymbolicTesting.Core;

/// <summary>
/// Represents the state of symbolic execution at a point in the program
/// </summary>
public class ExecutionState
{
    private readonly ImmutableDictionary<string, SymbolicValue> _variables;
    private readonly ImmutableStack<SymbolicValue> _evaluationStack;

    public PathCondition PathCondition { get; }
    public int InstructionPointer { get; init; }
    public string MethodName { get; init; }

    public ExecutionState(string methodName)
    {
        MethodName = methodName;
        _variables = ImmutableDictionary<string, SymbolicValue>.Empty;
        _evaluationStack = ImmutableStack<SymbolicValue>.Empty;
        PathCondition = new PathCondition();
        InstructionPointer = 0;
    }

    private ExecutionState(
        string methodName,
        ImmutableDictionary<string, SymbolicValue> variables,
        ImmutableStack<SymbolicValue> evaluationStack,
        PathCondition pathCondition,
        int instructionPointer)
    {
        MethodName = methodName;
        _variables = variables;
        _evaluationStack = evaluationStack;
        PathCondition = pathCondition;
        InstructionPointer = instructionPointer;
    }

    /// <summary>
    /// Sets a variable in the state
    /// </summary>
    public ExecutionState SetVariable(string name, SymbolicValue value)
    {
        return new ExecutionState(
            MethodName,
            _variables.SetItem(name, value),
            _evaluationStack,
            PathCondition,
            InstructionPointer
        );
    }

    /// <summary>
    /// Gets a variable from the state
    /// </summary>
    public SymbolicValue? GetVariable(string name)
    {
        return _variables.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// Pushes a value onto the evaluation stack
    /// </summary>
    public ExecutionState Push(SymbolicValue value)
    {
        return new ExecutionState(
            MethodName,
            _variables,
            _evaluationStack.Push(value),
            PathCondition,
            InstructionPointer
        );
    }

    /// <summary>
    /// Pops a value from the evaluation stack
    /// </summary>
    public (ExecutionState State, SymbolicValue Value) Pop()
    {
        var value = _evaluationStack.Peek();
        var newStack = _evaluationStack.Pop();

        return (new ExecutionState(
            MethodName,
            _variables,
            newStack,
            PathCondition,
            InstructionPointer
        ), value);
    }

    /// <summary>
    /// Adds a path constraint
    /// </summary>
    public ExecutionState AddConstraint(SymbolicValue condition, bool isTrueBranch)
    {
        return new ExecutionState(
            MethodName,
            _variables,
            _evaluationStack,
            PathCondition.AddConstraint(condition, isTrueBranch),
            InstructionPointer
        );
    }

    /// <summary>
    /// Moves to the next instruction
    /// </summary>
    public ExecutionState NextInstruction()
    {
        return new ExecutionState(
            MethodName,
            _variables,
            _evaluationStack,
            PathCondition,
            InstructionPointer + 1
        );
    }

    /// <summary>
    /// Jumps to a specific instruction
    /// </summary>
    public ExecutionState JumpTo(int instructionPointer)
    {
        return new ExecutionState(
            MethodName,
            _variables,
            _evaluationStack,
            PathCondition,
            instructionPointer
        );
    }

    public IReadOnlyDictionary<string, SymbolicValue> Variables => _variables;
    public IReadOnlyList<SymbolicValue> Stack => _evaluationStack.ToArray();
}
