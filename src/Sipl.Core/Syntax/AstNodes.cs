using Sipl.Core.Diagnostics;

namespace Sipl.Core.Syntax;

public abstract record AstNode(SourceSpan Span);


public abstract record ArithExpr(SourceSpan Span) : AstNode(Span);

public sealed record NumberExpr(int Value, SourceSpan Span) : ArithExpr(Span);

public sealed record VariableExpr(string Name, SourceSpan Span) : ArithExpr(Span);

public enum UnaryArithOperator
{
    Negate
}

public sealed record UnaryArithExpr(
    UnaryArithOperator Operator,
    ArithExpr Operand,
    SourceSpan Span) : ArithExpr(Span);

public enum BinaryArithOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo
}

public sealed record BinaryArithExpr(
    ArithExpr Left,
    BinaryArithOperator Operator,
    ArithExpr Right,
    SourceSpan Span) : ArithExpr(Span);


public abstract record BoolExpr(SourceSpan Span) : AstNode(Span);

public sealed record BoolLiteralExpr(bool Value, SourceSpan Span) : BoolExpr(Span);

public enum RelOperator
{
    Equal,          // =
    NotEqual,       // <> або !=
    Less,           // <
    LessOrEqual,    // <=
    Greater,        // >
    GreaterOrEqual  // >=
}

public sealed record ComparisonExpr(
    ArithExpr Left,
    RelOperator Operator,
    ArithExpr Right,
    SourceSpan Span) : BoolExpr(Span);

public sealed record NotExpr(BoolExpr Operand, SourceSpan Span) : BoolExpr(Span);

public enum LogicalOperator
{
    And,
    Or
}

public sealed record LogicalExpr(
    BoolExpr Left,
    LogicalOperator Operator,
    BoolExpr Right,
    SourceSpan Span) : BoolExpr(Span);


public abstract record Stmt(SourceSpan Span) : AstNode(Span);

public sealed record SkipStmt(SourceSpan Span) : Stmt(Span);

public sealed record AssignStmt(
    string VariableName,
    SourceSpan VariableSpan,
    ArithExpr Expression,
    SourceSpan Span) : Stmt(Span);

public sealed record SequenceStmt(
    IReadOnlyList<Stmt> Statements,
    SourceSpan Span) : Stmt(Span);

public sealed record IfStmt(
    BoolExpr Condition,
    Stmt ThenBranch,
    Stmt? ElseBranch,
    SourceSpan Span) : Stmt(Span);

public sealed record WhileStmt(
    BoolExpr Condition,
    Stmt Body,
    SourceSpan Span) : Stmt(Span);

public sealed record BlockStmt(
    Stmt InnerStatement,
    SourceSpan Span) : Stmt(Span);

public sealed record ProgramNode(
    Stmt Body,
    SourceSpan Span) : AstNode(Span);