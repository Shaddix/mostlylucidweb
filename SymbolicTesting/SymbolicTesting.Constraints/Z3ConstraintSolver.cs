using Microsoft.Z3;
using SymbolicTesting.Core;

namespace SymbolicTesting.Constraints;

/// <summary>
/// Constraint solver using Z3 SMT solver
/// </summary>
public class Z3ConstraintSolver : IDisposable
{
    private readonly Context _context;
    private readonly Dictionary<string, Expr> _variableCache = new();

    public Z3ConstraintSolver()
    {
        var config = new Dictionary<string, string>
        {
            { "model", "true" },
            { "proof", "false" }
        };
        _context = new Context(config);
    }

    /// <summary>
    /// Attempts to find concrete values that satisfy the given path condition
    /// </summary>
    public ConstraintSolution? Solve(PathCondition pathCondition)
    {
        _variableCache.Clear();

        var solver = _context.MkSolver();

        // Convert each constraint to Z3
        foreach (var constraint in pathCondition.Constraints)
        {
            var z3Expr = ConvertToZ3(constraint.Condition);

            if (z3Expr is BoolExpr boolExpr)
            {
                if (!constraint.IsTrueBranch)
                    boolExpr = _context.MkNot(boolExpr);

                solver.Assert(boolExpr);
            }
        }

        // Check satisfiability
        var status = solver.Check();

        if (status == Status.SATISFIABLE)
        {
            var model = solver.Model;
            var solution = new ConstraintSolution
            {
                IsSatisfiable = true,
                Values = ExtractValues(model)
            };
            return solution;
        }

        return new ConstraintSolution
        {
            IsSatisfiable = false,
            Values = new Dictionary<string, object?>()
        };
    }

    private Expr ConvertToZ3(SymbolicValue value)
    {
        return value switch
        {
            ConcreteValue concrete => ConvertConcrete(concrete),
            SymbolicVariable variable => GetOrCreateVariable(variable),
            SymbolicExpression expression => ConvertExpression(expression),
            _ => throw new NotSupportedException($"Unsupported symbolic value type: {value.GetType()}")
        };
    }

    private Expr ConvertConcrete(ConcreteValue concrete)
    {
        return concrete.Type.Name switch
        {
            "Int32" => _context.MkInt((int)(concrete.Value ?? 0)),
            "Int64" => _context.MkInt((long)(concrete.Value ?? 0L)),
            "Boolean" => (bool)(concrete.Value ?? false) ? _context.MkTrue() : _context.MkFalse(),
            "String" => _context.MkString((string)(concrete.Value ?? "")),
            "Double" => _context.MkReal(((double)(concrete.Value ?? 0.0)).ToString()),
            _ => throw new NotSupportedException($"Unsupported concrete type: {concrete.Type}")
        };
    }

    private Expr GetOrCreateVariable(SymbolicVariable variable)
    {
        var key = $"{variable.Name}${variable.Id}";

        if (_variableCache.TryGetValue(key, out var existing))
            return existing;

        Expr z3Var = variable.Type.Name switch
        {
            "Int32" or "Int64" => _context.MkIntConst(key),
            "Boolean" => _context.MkBoolConst(key),
            "String" => _context.MkConst(key, _context.MkStringSort()),
            "Double" => _context.MkRealConst(key),
            _ => _context.MkConst(key, _context.MkUninterpretedSort(variable.Type.Name))
        };

        _variableCache[key] = z3Var;
        return z3Var;
    }

    private Expr ConvertExpression(SymbolicExpression expression)
    {
        var operands = expression.Operands.Select(ConvertToZ3).ToArray();

        return expression.Operator switch
        {
            SymbolicOperator.Add when operands[0] is IntExpr => _context.MkAdd((ArithExpr)operands[0], (ArithExpr)operands[1]),
            SymbolicOperator.Subtract when operands[0] is IntExpr => _context.MkSub((ArithExpr)operands[0], (ArithExpr)operands[1]),
            SymbolicOperator.Multiply when operands[0] is IntExpr => _context.MkMul((ArithExpr)operands[0], (ArithExpr)operands[1]),
            SymbolicOperator.Divide when operands[0] is IntExpr => _context.MkDiv((IntExpr)operands[0], (IntExpr)operands[1]),
            SymbolicOperator.Modulo when operands[0] is IntExpr => _context.MkMod((IntExpr)operands[0], (IntExpr)operands[1]),
            SymbolicOperator.Negate when operands[0] is IntExpr => _context.MkUnaryMinus((ArithExpr)operands[0]),

            SymbolicOperator.Equal when operands[0] is IntExpr => _context.MkEq(operands[0], operands[1]),
            SymbolicOperator.NotEqual when operands[0] is IntExpr => _context.MkNot(_context.MkEq(operands[0], operands[1])),
            SymbolicOperator.LessThan when operands[0] is IntExpr => _context.MkLt((ArithExpr)operands[0], (ArithExpr)operands[1]),
            SymbolicOperator.LessThanOrEqual when operands[0] is IntExpr => _context.MkLe((ArithExpr)operands[0], (ArithExpr)operands[1]),
            SymbolicOperator.GreaterThan when operands[0] is IntExpr => _context.MkGt((ArithExpr)operands[0], (ArithExpr)operands[1]),
            SymbolicOperator.GreaterThanOrEqual when operands[0] is IntExpr => _context.MkGe((ArithExpr)operands[0], (ArithExpr)operands[1]),

            SymbolicOperator.And when operands[0] is BoolExpr => _context.MkAnd((BoolExpr)operands[0], (BoolExpr)operands[1]),
            SymbolicOperator.Or when operands[0] is BoolExpr => _context.MkOr((BoolExpr)operands[0], (BoolExpr)operands[1]),
            SymbolicOperator.Not when operands[0] is BoolExpr => _context.MkNot((BoolExpr)operands[0]),

            _ => throw new NotSupportedException($"Unsupported operator: {expression.Operator}")
        };
    }

    private Dictionary<string, object?> ExtractValues(Model model)
    {
        var values = new Dictionary<string, object?>();

        foreach (var kvp in _variableCache)
        {
            var name = kvp.Key;
            var z3Var = kvp.Value;

            var evaluation = model.Eval(z3Var, true);

            object? value = z3Var switch
            {
                IntExpr => int.Parse(evaluation.ToString()),
                BoolExpr => evaluation.IsTrue,
                _ => evaluation.ToString()
            };

            values[name] = value;
        }

        return values;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

/// <summary>
/// Result of constraint solving
/// </summary>
public class ConstraintSolution
{
    public required bool IsSatisfiable { get; init; }
    public required Dictionary<string, object?> Values { get; init; }
}
