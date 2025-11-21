# Architecture Documentation

## Overview

This document describes the internal architecture of the Symbolic Testing Framework.

## Core Concepts

### Symbolic Values

The foundation of symbolic execution is representing program values symbolically:

```csharp
// Base class for all symbolic values
public abstract record SymbolicValue
{
    public required Type Type { get; init; }
    public abstract bool IsConcrete { get; }
}

// Concrete value (known)
public record ConcreteValue : SymbolicValue
{
    public required object? Value { get; init; }
}

// Symbolic variable (unknown, to be explored)
public record SymbolicVariable : SymbolicValue
{
    public required string Name { get; init; }
    public int Id { get; init; }
}

// Symbolic expression (operations on symbolic values)
public record SymbolicExpression : SymbolicValue
{
    public required SymbolicOperator Operator { get; init; }
    public required IReadOnlyList<SymbolicValue> Operands { get; init; }
}
```

Example:
```csharp
// Concrete: x = 5
ConcreteValue { Value = 5, Type = typeof(int) }

// Symbolic: x (unknown)
SymbolicVariable { Name = "x", Id = 0, Type = typeof(int) }

// Expression: x + 5
SymbolicExpression {
    Operator = Add,
    Operands = [SymbolicVariable("x"), ConcreteValue(5)]
}
```

### Execution State

Represents the program state at any point during symbolic execution:

```csharp
public class ExecutionState
{
    // Variable bindings (local variables, parameters)
    private ImmutableDictionary<string, SymbolicValue> _variables;

    // Evaluation stack (for stack-based IL)
    private ImmutableStack<SymbolicValue> _evaluationStack;

    // Path constraints accumulated so far
    public PathCondition PathCondition { get; }

    // Current instruction pointer
    public int InstructionPointer { get; }
}
```

Execution state is **immutable** - each operation returns a new state. This allows easy path forking during exploration.

### Path Conditions

Track constraints that must be satisfied for a path:

```csharp
public record PathConstraint
{
    public required SymbolicValue Condition { get; init; }
    public required bool IsTrueBranch { get; init; }
}

public class PathCondition
{
    private ImmutableList<PathConstraint> _constraints;

    public PathCondition AddConstraint(SymbolicValue condition, bool isTrueBranch)
    {
        // Returns new path condition with added constraint
    }
}
```

Example path condition:
```
x > 0 && y < 10 && x != y
```

Represented as:
```csharp
PathCondition {
    Constraints = [
        { Condition = (x > 0), IsTrueBranch = true },
        { Condition = (y < 10), IsTrueBranch = true },
        { Condition = (x == y), IsTrueBranch = false }
    ]
}
```

## Component Architecture

### 1. IL Analysis (SymbolicTesting.Analysis)

**Purpose**: Parse and analyze .NET IL code

**Key Classes**:

```csharp
// Main analyzer
public class MethodAnalyzer
{
    public MethodAnalysisResult Analyze(MethodInfo method)
    {
        // 1. Load assembly with Mono.Cecil
        // 2. Find method definition
        // 3. Parse IL instructions
        // 4. Build basic blocks
        // 5. Build control flow graph
    }
}

// Basic block: sequence of instructions with single entry/exit
public class BasicBlock
{
    public int Id { get; }
    public List<Instruction> Instructions { get; }
}

// Control flow graph: directed graph of basic blocks
public class ControlFlowGraph
{
    public IReadOnlyList<BasicBlock> Blocks { get; }
    public IReadOnlyList<(BasicBlock Target, BranchType)> GetSuccessors(BasicBlock block);
}
```

**Control Flow Graph Example**:

```csharp
if (x > 0)      // Block 0: Load x, Load 0, Compare, Branch
    y = x + 1;  // Block 1: Load x, Load 1, Add, Store y
else
    y = x - 1;  // Block 2: Load x, Load 1, Sub, Store y
return y;       // Block 3: Load y, Return

// CFG:
// Block 0 --true--> Block 1 --unconditional--> Block 3
//        |
//        --false--> Block 2 --unconditional--> Block 3
```

### 2. Constraint Solving (SymbolicTesting.Constraints)

**Purpose**: Use Z3 SMT solver to find concrete values

**Key Class**:

```csharp
public class Z3ConstraintSolver
{
    // Converts symbolic values to Z3 expressions
    private Expr ConvertToZ3(SymbolicValue value);

    // Solves constraints and returns concrete values
    public ConstraintSolution? Solve(PathCondition pathCondition)
    {
        // 1. Create Z3 context
        // 2. Convert path constraints to Z3
        // 3. Call Z3 solver
        // 4. Extract model (concrete values)
        // 5. Return solution
    }
}
```

**Z3 Integration Example**:

```csharp
// Path constraint: x > 5 && x < 10
PathCondition {
    (x > 5) = true,
    (x < 10) = true
}

// Z3 converts to:
ctx.MkAnd(
    ctx.MkGt(x_var, ctx.MkInt(5)),
    ctx.MkLt(x_var, ctx.MkInt(10))
)

// Z3 returns: x = 7 (one possible solution)
```

### 3. Symbolic Execution Engine (SymbolicTesting.Core)

**Purpose**: Execute IL instructions symbolically

**Main Loop**:

```csharp
public class SymbolicExecutor
{
    public async Task<ExplorationResult> ExploreAsync(MethodInfo method)
    {
        // 1. Analyze method
        var analysis = _analyzer.Analyze(method);

        // 2. Create initial state with symbolic parameters
        var initialState = CreateInitialState(method);

        // 3. Work queue for path exploration
        var workQueue = new Queue<(ExecutionState, BasicBlock)>();
        workQueue.Enqueue((initialState, firstBlock));

        // 4. Explore paths
        while (workQueue.Count > 0)
        {
            var (state, block) = workQueue.Dequeue();

            // Execute block symbolically
            var result = ExecuteBlock(state, block);

            if (result.IsCompleted)
            {
                // Solve constraints and generate test case
                var solution = _solver.Solve(state.PathCondition);
                testCases.Add(CreateTestCase(solution));
            }
            else
            {
                // Add successor states to queue
                foreach (var (nextState, nextBlock) in result.NextBlocks)
                    workQueue.Enqueue((nextState, nextBlock));
            }
        }

        return new ExplorationResult { TestCases = testCases };
    }
}
```

**Instruction Execution**:

```csharp
private ExecutionState ExecuteInstruction(ExecutionState state, Instruction inst)
{
    return inst.OpCode.Code switch
    {
        // Load argument
        Code.Ldarg => state.Push(state.GetVariable(argName)),

        // Load constant
        Code.Ldc_I4 => state.Push(new ConcreteValue { Value = operand }),

        // Add
        Code.Add => {
            var (s1, right) = state.Pop();
            var (s2, left) = s1.Pop();
            var expr = new SymbolicExpression {
                Operator = Add,
                Operands = [left, right]
            };
            return s2.Push(expr);
        },

        // Compare equal
        Code.Ceq => {
            var (s1, right) = state.Pop();
            var (s2, left) = s1.Pop();
            var expr = new SymbolicExpression {
                Operator = Equal,
                Operands = [left, right]
            };
            return s2.Push(expr);
        },

        // ... more instructions ...
    };
}
```

### 4. Test Generation (SymbolicTesting.TestGeneration)

**Purpose**: Generate C# test code using Roslyn

```csharp
public class TestGenerator
{
    public string GenerateTestClass(Type type, MethodInfo method, ExplorationResult result)
    {
        // Use Roslyn syntax trees to generate:
        // - Test class declaration
        // - Test methods (one per test case)
        // - Arrange/Act/Assert structure
    }
}
```

**Generated Code Example**:

```csharp
// Input: TestCase { Inputs = { x = 5, y = 10 } }

// Generated:
[Fact]
public void Test_MyMethod_0()
{
    var instance = new MyClass();
    var result = instance.MyMethod(5, 10);
    Assert.NotNull(result);
}
```

### 5. xUnit Integration (SymbolicTesting.XUnit)

**Purpose**: Provide xUnit attributes for easy integration

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class SymbolicDataAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        // 1. Find target method to test
        // 2. Run symbolic execution
        // 3. Convert test cases to xUnit theory data
        // 4. Yield test inputs
    }
}
```

## Path Exploration Strategies

### Depth-First Search (DFS)

```
Stack-based exploration:
1. Push initial state
2. Pop state, execute
3. Push successor states
4. Repeat
```

Pros: Memory efficient, finds deep bugs
Cons: May miss some paths if timeout occurs

### Breadth-First Search (BFS)

```
Queue-based exploration:
1. Enqueue initial state
2. Dequeue state, execute
3. Enqueue successor states
4. Repeat
```

Pros: Explores all paths at same depth
Cons: High memory usage

### Coverage-Guided

Prioritize paths that cover new code:

```
1. Track covered instructions
2. Score paths by new coverage
3. Explore highest-scoring paths first
```

## Data Flow

```
Method (MethodInfo)
    |
    v
[IL Analysis]
    |
    v
Control Flow Graph + Instructions
    |
    v
[Symbolic Execution]
    |
    +---> Execution State
    |         |
    |         v
    |     [Branching]
    |         |
    |         +---> Path Condition 1
    |         |
    |         +---> Path Condition 2
    |
    v
[Constraint Solving]
    |
    v
Concrete Values (Test Inputs)
    |
    v
[Test Generation]
    |
    v
xUnit Test Methods
```

## Immutability and State Management

All core data structures are immutable:

```csharp
// ✓ Correct: Returns new state
var newState = state.Push(value);

// ✗ Wrong: Mutates state
state.Stack.Push(value);
```

Benefits:
- Easy path forking (just copy reference)
- Thread-safe
- Easier to reason about
- Can implement undo/redo

## Performance Considerations

### Path Explosion

Problem: Number of paths grows exponentially with branches

```csharp
if (a) ... // 2 paths
if (b) ... // 4 paths
if (c) ... // 8 paths
// ... after 20 conditions: 1 million paths!
```

Solutions:
- Limit max paths explored
- Use timeouts
- Smart path prioritization
- Path merging (future work)

### Constraint Solving Time

Z3 solving can be slow for complex constraints.

Solutions:
- Cache similar constraints
- Simplify constraints before solving
- Use faster theories (integers vs reals)
- Timeout individual solver calls

### Memory Usage

Storing many execution states uses memory.

Solutions:
- Immutable data structures (structural sharing)
- Limit work queue size
- Garbage collect unreachable states

## Extension Points

### Adding New IL Instructions

```csharp
// In SymbolicExecutor.ExecuteInstruction()
case Code.NewInstruction =>
    // 1. Pop operands from stack
    // 2. Create symbolic expression
    // 3. Push result
    return state.Push(result);
```

### Custom Constraint Solvers

Implement alternative to Z3:

```csharp
public interface IConstraintSolver
{
    ConstraintSolution? Solve(PathCondition pathCondition);
}
```

### Custom Path Strategies

```csharp
public interface IPathStrategy
{
    (ExecutionState, BasicBlock)? SelectNext(
        IEnumerable<(ExecutionState, BasicBlock)> candidates);
}
```

## Future Enhancements

1. **Concolic Execution**: Mix concrete and symbolic execution
2. **Loop Handling**: Bounded loop unrolling
3. **Object Support**: Heap modeling
4. **Floating Point**: Support for float/double
5. **String Operations**: Symbolic string constraints
6. **Parallel Exploration**: Multi-threaded path exploration
7. **Path Merging**: Reduce path explosion
8. **Coverage Feedback**: Learn from previous runs

## References

- [SAGE: Whitebox Fuzzing for Security Testing](https://queue.acm.org/detail.cfm?id=2094081)
- [KLEE: Unassisted and Automatic Generation of High-Coverage Tests](https://llvm.org/pubs/2008-12-OSDI-KLEE.pdf)
- [PEX: White Box Test Generation for .NET](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/pex-tap.pdf)
