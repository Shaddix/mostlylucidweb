namespace SymbolicTesting.Core;

/// <summary>
/// Represents a symbolic value that can be concrete or symbolic
/// </summary>
public abstract record SymbolicValue
{
    /// <summary>
    /// The type of this symbolic value
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// True if this value is concrete (not symbolic)
    /// </summary>
    public abstract bool IsConcrete { get; }
}

/// <summary>
/// A concrete value (known at analysis time)
/// </summary>
public record ConcreteValue : SymbolicValue
{
    public required object? Value { get; init; }
    public override bool IsConcrete => true;

    public override string ToString() => Value?.ToString() ?? "null";
}

/// <summary>
/// A symbolic variable (unknown value to be explored)
/// </summary>
public record SymbolicVariable : SymbolicValue
{
    public required string Name { get; init; }
    public int Id { get; init; }
    public override bool IsConcrete => false;

    public override string ToString() => $"{Name}${Id}";
}

/// <summary>
/// A symbolic expression combining values with operations
/// </summary>
public record SymbolicExpression : SymbolicValue
{
    public required SymbolicOperator Operator { get; init; }
    public required IReadOnlyList<SymbolicValue> Operands { get; init; }
    public override bool IsConcrete => false;

    public override string ToString()
    {
        return Operator switch
        {
            SymbolicOperator.Add => $"({Operands[0]} + {Operands[1]})",
            SymbolicOperator.Subtract => $"({Operands[0]} - {Operands[1]})",
            SymbolicOperator.Multiply => $"({Operands[0]} * {Operands[1]})",
            SymbolicOperator.Divide => $"({Operands[0]} / {Operands[1]})",
            SymbolicOperator.Modulo => $"({Operands[0]} % {Operands[1]})",
            SymbolicOperator.Equal => $"({Operands[0]} == {Operands[1]})",
            SymbolicOperator.NotEqual => $"({Operands[0]} != {Operands[1]})",
            SymbolicOperator.LessThan => $"({Operands[0]} < {Operands[1]})",
            SymbolicOperator.LessThanOrEqual => $"({Operands[0]} <= {Operands[1]})",
            SymbolicOperator.GreaterThan => $"({Operands[0]} > {Operands[1]})",
            SymbolicOperator.GreaterThanOrEqual => $"({Operands[0]} >= {Operands[1]})",
            SymbolicOperator.And => $"({Operands[0]} && {Operands[1]})",
            SymbolicOperator.Or => $"({Operands[0]} || {Operands[1]})",
            SymbolicOperator.Not => $"!{Operands[0]}",
            SymbolicOperator.Negate => $"-{Operands[0]}",
            SymbolicOperator.BitwiseAnd => $"({Operands[0]} & {Operands[1]})",
            SymbolicOperator.BitwiseOr => $"({Operands[0]} | {Operands[1]})",
            SymbolicOperator.BitwiseXor => $"({Operands[0]} ^ {Operands[1]})",
            SymbolicOperator.BitwiseNot => $"~{Operands[0]}",
            SymbolicOperator.LeftShift => $"({Operands[0]} << {Operands[1]})",
            SymbolicOperator.RightShift => $"({Operands[0]} >> {Operands[1]})",
            _ => $"{Operator}({string.Join(", ", Operands)})"
        };
    }
}

/// <summary>
/// Operations that can be performed on symbolic values
/// </summary>
public enum SymbolicOperator
{
    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Negate,

    // Comparison
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    // Logical
    And,
    Or,
    Not,

    // Bitwise
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    BitwiseNot,
    LeftShift,
    RightShift,

    // Type operations
    Cast,
    IsInstance
}
