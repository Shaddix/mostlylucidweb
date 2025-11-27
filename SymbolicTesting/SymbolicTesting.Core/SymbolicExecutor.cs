using System.Diagnostics;
using System.Reflection;
using SymbolicTesting.Analysis;
using SymbolicTesting.Constraints;

namespace SymbolicTesting.Core;

/// <summary>
/// Main symbolic execution engine
/// </summary>
public class SymbolicExecutor : ISymbolicExecutor
{
    private readonly MethodAnalyzer _analyzer = new();
    private int _nextSymbolicId = 0;

    public async Task<ExplorationResult> ExploreAsync(
        MethodInfo method,
        ExplorationOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var testCases = new List<TestCase>();
        var pathsExplored = 0;
        var pathsSatisfiable = 0;

        // Analyze the method
        var analysis = _analyzer.Analyze(method);

        // Create initial execution state with symbolic parameters
        var initialState = CreateInitialState(method, analysis);

        // Work queue for path exploration
        var workQueue = new Queue<(ExecutionState State, int BlockIndex)>();
        workQueue.Enqueue((initialState, 0));

        using var solver = new Z3ConstraintSolver();

        while (workQueue.Count > 0 && pathsExplored < options.MaxPaths)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (stopwatch.Elapsed > options.Timeout)
                break;

            var (state, blockIndex) = workQueue.Dequeue();

            if (blockIndex >= analysis.BasicBlocks.Count)
                continue;

            var block = analysis.BasicBlocks[blockIndex];

            // Execute the basic block symbolically
            var result = await ExecuteBlockAsync(state, block, analysis);

            pathsExplored++;

            if (result.IsCompleted)
            {
                // Path completed - try to solve constraints and generate test case
                var solution = solver.Solve(result.State.PathCondition);

                if (solution?.IsSatisfiable == true)
                {
                    pathsSatisfiable++;
                    var testCase = CreateTestCase(method, analysis, solution);
                    testCases.Add(testCase);
                }
            }
            else if (result.NextBlocks.Count > 0)
            {
                // Add successor states to work queue
                foreach (var (nextState, nextBlock) in result.NextBlocks)
                {
                    var nextBlockIndex = analysis.BasicBlocks.IndexOf(nextBlock);
                    if (nextBlockIndex >= 0)
                        workQueue.Enqueue((nextState, nextBlockIndex));
                }
            }
        }

        stopwatch.Stop();

        return new ExplorationResult
        {
            TestCases = testCases,
            PathsExplored = pathsExplored,
            PathsSatisfiable = pathsSatisfiable,
            ExecutionTime = stopwatch.Elapsed
        };
    }

    private ExecutionState CreateInitialState(MethodInfo method, MethodAnalysisResult analysis)
    {
        var state = new ExecutionState(method.Name);

        // Create symbolic variables for each parameter
        foreach (var param in analysis.Parameters)
        {
            var symbolicVar = new SymbolicVariable
            {
                Name = param.Name,
                Id = _nextSymbolicId++,
                Type = param.Type
            };

            state = state.SetVariable(param.Name, symbolicVar);
        }

        return state;
    }

    private Task<BlockExecutionResult> ExecuteBlockAsync(
        ExecutionState state,
        BasicBlock block,
        MethodAnalysisResult analysis)
    {
        var currentState = state;

        try
        {
            // Execute each instruction in the block
            foreach (var instruction in block.Instructions)
            {
                currentState = ExecuteInstruction(currentState, instruction);
            }

            // Determine successor blocks
            var successors = analysis.ControlFlowGraph.GetSuccessors(block);
            var nextBlocks = new List<(ExecutionState, BasicBlock)>();

            foreach (var (successor, branchType) in successors)
            {
                if (branchType == BranchType.Unconditional)
                {
                    nextBlocks.Add((currentState, successor));
                }
                else
                {
                    // For conditional branches, pop the condition and create two paths
                    var (stateAfterPop, condition) = currentState.Pop();

                    // True branch
                    if (branchType == BranchType.True)
                    {
                        var trueState = stateAfterPop.AddConstraint(condition, true);
                        nextBlocks.Add((trueState, successor));
                    }

                    // False branch
                    if (branchType == BranchType.False)
                    {
                        var falseState = stateAfterPop.AddConstraint(condition, false);
                        nextBlocks.Add((falseState, successor));
                    }
                }
            }

            // Check if this is a return instruction
            var lastInstruction = block.Instructions.Last();
            bool isReturn = lastInstruction.OpCode.FlowControl == Mono.Cecil.Cil.FlowControl.Return;

            return Task.FromResult(new BlockExecutionResult
            {
                State = currentState,
                IsCompleted = isReturn,
                NextBlocks = nextBlocks
            });
        }
        catch (Exception ex)
        {
            // If we can't execute symbolically, mark as completed to avoid infinite loops
            return Task.FromResult(new BlockExecutionResult
            {
                State = currentState,
                IsCompleted = true,
                NextBlocks = new List<(ExecutionState, BasicBlock)>()
            });
        }
    }

    private ExecutionState ExecuteInstruction(ExecutionState state, Mono.Cecil.Cil.Instruction instruction)
    {
        var opCode = instruction.OpCode;

        // Handle different instruction types
        if (opCode.Code == Mono.Cecil.Cil.Code.Ldarg || opCode.Code == Mono.Cecil.Cil.Code.Ldarg_0 ||
            opCode.Code == Mono.Cecil.Cil.Code.Ldarg_1 || opCode.Code == Mono.Cecil.Cil.Code.Ldarg_2 ||
            opCode.Code == Mono.Cecil.Cil.Code.Ldarg_3 || opCode.Code == Mono.Cecil.Cil.Code.Ldarg_S)
        {
            // Load argument
            int argIndex = opCode.Code switch
            {
                Mono.Cecil.Cil.Code.Ldarg_0 => 0,
                Mono.Cecil.Cil.Code.Ldarg_1 => 1,
                Mono.Cecil.Cil.Code.Ldarg_2 => 2,
                Mono.Cecil.Cil.Code.Ldarg_3 => 3,
                _ => instruction.Operand is int i ? i : 0
            };

            var argName = $"arg{argIndex}";
            var value = state.GetVariable(argName);

            if (value != null)
                return state.Push(value);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4 || opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_0 ||
                 opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_1 || opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_2 ||
                 opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_3 || opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_4 ||
                 opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_5 || opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_6 ||
                 opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_7 || opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_8 ||
                 opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_S || opCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_M1)
        {
            // Load constant integer
            int value = opCode.Code switch
            {
                Mono.Cecil.Cil.Code.Ldc_I4_M1 => -1,
                Mono.Cecil.Cil.Code.Ldc_I4_0 => 0,
                Mono.Cecil.Cil.Code.Ldc_I4_1 => 1,
                Mono.Cecil.Cil.Code.Ldc_I4_2 => 2,
                Mono.Cecil.Cil.Code.Ldc_I4_3 => 3,
                Mono.Cecil.Cil.Code.Ldc_I4_4 => 4,
                Mono.Cecil.Cil.Code.Ldc_I4_5 => 5,
                Mono.Cecil.Cil.Code.Ldc_I4_6 => 6,
                Mono.Cecil.Cil.Code.Ldc_I4_7 => 7,
                Mono.Cecil.Cil.Code.Ldc_I4_8 => 8,
                _ => instruction.Operand is int i ? i : (instruction.Operand is sbyte sb ? sb : 0)
            };

            return state.Push(new ConcreteValue { Value = value, Type = typeof(int) });
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Add)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.Add,
                Operands = new[] { left, right },
                Type = left.Type
            };

            return state2.Push(result);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Sub)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.Subtract,
                Operands = new[] { left, right },
                Type = left.Type
            };

            return state2.Push(result);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Mul)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.Multiply,
                Operands = new[] { left, right },
                Type = left.Type
            };

            return state2.Push(result);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Div)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.Divide,
                Operands = new[] { left, right },
                Type = left.Type
            };

            return state2.Push(result);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Ceq)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.Equal,
                Operands = new[] { left, right },
                Type = typeof(bool)
            };

            return state2.Push(result);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Cgt)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.GreaterThan,
                Operands = new[] { left, right },
                Type = typeof(bool)
            };

            return state2.Push(result);
        }
        else if (opCode.Code == Mono.Cecil.Cil.Code.Clt)
        {
            var (state1, right) = state.Pop();
            var (state2, left) = state1.Pop();

            var result = new SymbolicExpression
            {
                Operator = SymbolicOperator.LessThan,
                Operands = new[] { left, right },
                Type = typeof(bool)
            };

            return state2.Push(result);
        }

        return state;
    }

    private TestCase CreateTestCase(
        MethodInfo method,
        MethodAnalysisResult analysis,
        ConstraintSolution solution)
    {
        var inputs = new Dictionary<string, object?>();

        foreach (var param in analysis.Parameters)
        {
            var key = $"{param.Name}$0"; // Assuming ID 0 for first variable
            if (solution.Values.TryGetValue(key, out var value))
            {
                inputs[param.Name] = value;
            }
        }

        return new TestCase
        {
            MethodName = method.Name,
            Inputs = inputs,
            PathCondition = new PathCondition() // Could be filled in
        };
    }
}

internal class BlockExecutionResult
{
    public required ExecutionState State { get; init; }
    public required bool IsCompleted { get; init; }
    public required List<(ExecutionState State, BasicBlock Block)> NextBlocks { get; init; }
}
