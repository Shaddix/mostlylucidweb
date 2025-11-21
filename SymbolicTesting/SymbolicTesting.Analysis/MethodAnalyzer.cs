using Mono.Cecil;
using Mono.Cecil.Cil;
using SymbolicTesting.Core;

namespace SymbolicTesting.Analysis;

/// <summary>
/// Analyzes method IL code for symbolic execution
/// </summary>
public class MethodAnalyzer
{
    /// <summary>
    /// Analyzes a method and returns its IL instructions and control flow
    /// </summary>
    public MethodAnalysisResult Analyze(System.Reflection.MethodInfo method)
    {
        // Load the assembly using Mono.Cecil
        var assemblyPath = method.DeclaringType!.Assembly.Location;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);

        // Find the method definition
        var typeDefinition = assemblyDefinition.MainModule.Types
            .FirstOrDefault(t => t.FullName == method.DeclaringType.FullName);

        if (typeDefinition == null)
            throw new InvalidOperationException($"Could not find type {method.DeclaringType.FullName}");

        var methodDefinition = typeDefinition.Methods
            .FirstOrDefault(m => MatchesMethod(m, method));

        if (methodDefinition == null)
            throw new InvalidOperationException($"Could not find method {method.Name}");

        // Analyze the method body
        if (methodDefinition.Body == null)
            throw new InvalidOperationException($"Method {method.Name} has no body");

        var instructions = methodDefinition.Body.Instructions.ToList();
        var basicBlocks = BuildBasicBlocks(instructions);
        var controlFlow = BuildControlFlowGraph(basicBlocks);

        return new MethodAnalysisResult
        {
            Method = method,
            MethodDefinition = methodDefinition,
            Instructions = instructions,
            BasicBlocks = basicBlocks,
            ControlFlowGraph = controlFlow,
            Parameters = method.GetParameters().Select(p => new ParameterInfo
            {
                Name = p.Name ?? $"arg{p.Position}",
                Type = p.ParameterType,
                Position = p.Position
            }).ToList()
        };
    }

    private bool MatchesMethod(MethodDefinition cecilMethod, System.Reflection.MethodInfo reflectionMethod)
    {
        if (cecilMethod.Name != reflectionMethod.Name)
            return false;

        var cecilParams = cecilMethod.Parameters;
        var reflectionParams = reflectionMethod.GetParameters();

        if (cecilParams.Count != reflectionParams.Length)
            return false;

        for (int i = 0; i < cecilParams.Count; i++)
        {
            if (cecilParams[i].ParameterType.FullName != reflectionParams[i].ParameterType.FullName)
                return false;
        }

        return true;
    }

    private List<BasicBlock> BuildBasicBlocks(List<Instruction> instructions)
    {
        var blocks = new List<BasicBlock>();
        var leaders = new HashSet<int> { 0 }; // First instruction is always a leader

        // Find all leaders (instructions that start a new basic block)
        for (int i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];

            // Branch targets are leaders
            if (instruction.Operand is Instruction target)
            {
                var targetIndex = instructions.IndexOf(target);
                leaders.Add(targetIndex);

                // Instruction after branch is also a leader
                if (i + 1 < instructions.Count)
                    leaders.Add(i + 1);
            }

            // Instructions after branch are leaders
            if (instruction.OpCode.FlowControl == FlowControl.Branch ||
                instruction.OpCode.FlowControl == FlowControl.Cond_Branch ||
                instruction.OpCode.FlowControl == FlowControl.Return)
            {
                if (i + 1 < instructions.Count)
                    leaders.Add(i + 1);
            }
        }

        var sortedLeaders = leaders.OrderBy(x => x).ToList();

        // Create basic blocks
        for (int i = 0; i < sortedLeaders.Count; i++)
        {
            var start = sortedLeaders[i];
            var end = i + 1 < sortedLeaders.Count ? sortedLeaders[i + 1] : instructions.Count;

            blocks.Add(new BasicBlock
            {
                Id = i,
                StartOffset = start,
                EndOffset = end,
                Instructions = instructions.GetRange(start, end - start)
            });
        }

        return blocks;
    }

    private ControlFlowGraph BuildControlFlowGraph(List<BasicBlock> blocks)
    {
        var graph = new ControlFlowGraph();

        foreach (var block in blocks)
        {
            graph.AddBlock(block);

            var lastInstruction = block.Instructions.Last();

            // Add edges based on the last instruction
            if (lastInstruction.OpCode.FlowControl == FlowControl.Branch)
            {
                // Unconditional branch
                if (lastInstruction.Operand is Instruction target)
                {
                    var targetBlock = blocks.FirstOrDefault(b =>
                        b.Instructions.Any(i => i.Offset == target.Offset));

                    if (targetBlock != null)
                        graph.AddEdge(block, targetBlock, BranchType.Unconditional);
                }
            }
            else if (lastInstruction.OpCode.FlowControl == FlowControl.Cond_Branch)
            {
                // Conditional branch
                if (lastInstruction.Operand is Instruction target)
                {
                    var targetBlock = blocks.FirstOrDefault(b =>
                        b.Instructions.Any(i => i.Offset == target.Offset));

                    if (targetBlock != null)
                        graph.AddEdge(block, targetBlock, BranchType.True);
                }

                // Fall through to next block
                var nextBlock = blocks.FirstOrDefault(b => b.StartOffset == block.EndOffset);
                if (nextBlock != null)
                    graph.AddEdge(block, nextBlock, BranchType.False);
            }
            else if (lastInstruction.OpCode.FlowControl != FlowControl.Return)
            {
                // Fall through to next block
                var nextBlock = blocks.FirstOrDefault(b => b.StartOffset == block.EndOffset);
                if (nextBlock != null)
                    graph.AddEdge(block, nextBlock, BranchType.Unconditional);
            }
        }

        return graph;
    }
}

public class MethodAnalysisResult
{
    public required System.Reflection.MethodInfo Method { get; init; }
    public required MethodDefinition MethodDefinition { get; init; }
    public required List<Instruction> Instructions { get; init; }
    public required List<BasicBlock> BasicBlocks { get; init; }
    public required ControlFlowGraph ControlFlowGraph { get; init; }
    public required List<ParameterInfo> Parameters { get; init; }
}

public class ParameterInfo
{
    public required string Name { get; init; }
    public required Type Type { get; init; }
    public required int Position { get; init; }
}

public class BasicBlock
{
    public required int Id { get; init; }
    public required int StartOffset { get; init; }
    public required int EndOffset { get; init; }
    public required List<Instruction> Instructions { get; init; }

    public override string ToString() => $"BB{Id} [{StartOffset}..{EndOffset})";
}

public enum BranchType
{
    Unconditional,
    True,
    False
}

public class ControlFlowGraph
{
    private readonly Dictionary<BasicBlock, List<(BasicBlock Target, BranchType Type)>> _edges = new();
    private readonly List<BasicBlock> _blocks = new();

    public void AddBlock(BasicBlock block)
    {
        _blocks.Add(block);
        _edges[block] = new List<(BasicBlock, BranchType)>();
    }

    public void AddEdge(BasicBlock from, BasicBlock to, BranchType type)
    {
        if (!_edges.ContainsKey(from))
            _edges[from] = new List<(BasicBlock, BranchType)>();

        _edges[from].Add((to, type));
    }

    public IReadOnlyList<BasicBlock> Blocks => _blocks;

    public IReadOnlyList<(BasicBlock Target, BranchType Type)> GetSuccessors(BasicBlock block)
    {
        return _edges.TryGetValue(block, out var successors) ? successors : new List<(BasicBlock, BranchType)>();
    }
}
