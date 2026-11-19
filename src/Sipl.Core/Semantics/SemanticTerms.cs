using Sipl.Core.Syntax;

namespace Sipl.Core.Semantics;

public abstract record SemanticTerm
{
    public abstract string ToNotation();
}


public sealed record IdentityTerm : SemanticTerm
{
    public static readonly IdentityTerm Instance = new();
    public override string ToNotation() => "id";
}

public sealed record AssignTerm(string Variable, ArithExpr Expression) : SemanticTerm
{
    public override string ToNotation() => $"assign({Variable}, {ExpressionFormatter.FormatArith(Expression)})";
}

public sealed record CompositionTerm(SemanticTerm First, SemanticTerm Second) : SemanticTerm
{
    public override string ToNotation() => $"({Second.ToNotation()} ∘ {First.ToNotation()})";
}

public sealed record ConditionalTerm(
    BoolExpr Condition,
    SemanticTerm ThenTerm,
    SemanticTerm ElseTerm) : SemanticTerm
{
    public override string ToNotation() =>
        $"cond({ExpressionFormatter.FormatBool(Condition)}, {ThenTerm.ToNotation()}, {ElseTerm.ToNotation()})";
}

public sealed record FixpointTerm(
    BoolExpr Condition,
    SemanticTerm BodyTerm) : SemanticTerm
{
    public override string ToNotation() =>
        $"fix(λF. cond({ExpressionFormatter.FormatBool(Condition)}, F ∘ {BodyTerm.ToNotation()}, id))";
}

public static class SemanticTermBuilder
{
    public static SemanticTerm Build(ProgramNode program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return BuildFromStatement(program.Body);
    }

    public static SemanticTerm BuildFromStatement(Stmt stmt) => stmt switch
    {
        SkipStmt => IdentityTerm.Instance,

        AssignStmt assign => new AssignTerm(assign.VariableName, assign.Expression),

        BlockStmt block => BuildFromStatement(block.InnerStatement),

        SequenceStmt seq => BuildSequence(seq.Statements),

        IfStmt ifStmt => new ConditionalTerm(
            ifStmt.Condition,
            BuildFromStatement(ifStmt.ThenBranch),
            ifStmt.ElseBranch is not null ? BuildFromStatement(ifStmt.ElseBranch) : IdentityTerm.Instance),

        WhileStmt whileStmt => new FixpointTerm(
            whileStmt.Condition,
            BuildFromStatement(whileStmt.Body)),

        _ => throw new InvalidOperationException($"Невідомий вузол AST для побудови терму: {stmt.GetType().Name}")
    };

    private static SemanticTerm BuildSequence(IReadOnlyList<Stmt> statements)
    {
        if (statements.Count == 0)
            return IdentityTerm.Instance;

        var current = BuildFromStatement(statements[0]);
        for (int i = 1; i < statements.Count; i++)
        {
            var next = BuildFromStatement(statements[i]);
            current = new CompositionTerm(current, next);
        }
        return current;
    }
}

public static class ExpressionFormatter
{
    public static string FormatArith(ArithExpr expr) => expr switch
    {
        NumberExpr n => n.Value.ToString(),
        VariableExpr v => v.Name,
        UnaryArithExpr u => $"-({FormatArith(u.Operand)})",
        BinaryArithExpr b => $"({FormatArith(b.Left)} {FormatArithOp(b.Operator)} {FormatArith(b.Right)})",
        _ => expr.ToString() ?? string.Empty
    };

    public static string FormatBool(BoolExpr expr) => expr switch
    {
        BoolLiteralExpr bl => bl.Value ? "true" : "false",
        NotExpr n => $"not ({FormatBool(n.Operand)})",
        LogicalExpr l => $"({FormatBool(l.Left)} {(l.Operator == LogicalOperator.And ? "and" : "or")} {FormatBool(l.Right)})",
        ComparisonExpr c => $"({FormatArith(c.Left)} {FormatRelOp(c.Operator)} {FormatArith(c.Right)})",
        _ => expr.ToString() ?? string.Empty
    };

    private static string FormatArithOp(BinaryArithOperator op) => op switch
    {
        BinaryArithOperator.Add => "+",
        BinaryArithOperator.Subtract => "-",
        BinaryArithOperator.Multiply => "*",
        BinaryArithOperator.Divide => "/",
        BinaryArithOperator.Modulo => "%",
        _ => "?"
    };

    private static string FormatRelOp(RelOperator op) => op switch
    {
        RelOperator.Equal => "=",
        RelOperator.NotEqual => "<>",
        RelOperator.Less => "<",
        RelOperator.LessOrEqual => "<=",
        RelOperator.Greater => ">",
        RelOperator.GreaterOrEqual => ">=",
        _ => "?"
    };
}