using System.Text;
using Sipl.Core.Diagnostics;
using Sipl.Core.Syntax;

namespace Sipl.Core.Visualization;

public sealed record ParseTreeNode(
    string Symbol,
    bool IsTerminal,
    SourceSpan Span,
    IReadOnlyList<ParseTreeNode> Children,
    string? Lexeme = null);

public static class ParseTreeBuilder
{
    public static ParseTreeNode Build(ProgramNode program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var stmtNode = BuildStatement(program.Body);
        return NonTerminal("Program", program.Span, stmtNode);
    }

    public static string ToTextTree(ParseTreeNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var sb = new StringBuilder();
        WriteTextNode(root, sb, string.Empty, isLast: true);
        return sb.ToString();
    }

    private static void WriteTextNode(ParseTreeNode node, StringBuilder sb, string indent, bool isLast)
    {
        string connector = isLast ? "└── " : "├── ";
        string label = node.IsTerminal
            ? $"'{node.Lexeme}' ({node.Symbol})"
            : $"<{node.Symbol}>";

        sb.Append(indent);
        sb.Append(connector);
        sb.AppendLine($"{label} [{node.Span}]");

        string childIndent = indent + (isLast ? "    " : "│   ");
        for (int i = 0; i < node.Children.Count; i++)
        {
            WriteTextNode(node.Children[i], sb, childIndent, i == node.Children.Count - 1);
        }
    }

    private static ParseTreeNode BuildStatement(Stmt stmt)
    {
        var inner = stmt switch
        {
            SkipStmt s => NonTerminal("SkipStmt", s.Span, Terminal("KEYWORD", "skip", s.Span)),

            AssignStmt a => NonTerminal("AssignStmt", a.Span,
                Terminal("IDENT", a.VariableName, a.VariableSpan),
                Terminal("OP", ":=", a.VariableSpan),
                BuildArithExpr(a.Expression)),

            SequenceStmt seq => BuildSequence(seq),

            IfStmt i => BuildIf(i),

            WhileStmt w => NonTerminal("WhileStmt", w.Span,
                Terminal("KEYWORD", "while", w.Span),
                BuildBoolExpr(w.Condition),
                Terminal("KEYWORD", "do", w.Span),
                BuildStatement(w.Body)),

            BlockStmt b => NonTerminal("BlockStmt", b.Span,
                Terminal("KEYWORD", "begin", b.Span),
                BuildStatement(b.InnerStatement),
                Terminal("KEYWORD", "end", b.Span)),

            _ => NonTerminal(stmt.GetType().Name, stmt.Span)
        };

        return stmt is SequenceStmt
            ? NonTerminal("Statement", stmt.Span, inner)
            : NonTerminal("Statement", stmt.Span, NonTerminal("Sequence", stmt.Span, NonTerminal("SimpleStatement", stmt.Span, inner)));
    }

    private static ParseTreeNode BuildSequence(SequenceStmt seq)
    {
        var children = new List<ParseTreeNode>();
        for (int i = 0; i < seq.Statements.Count; i++)
        {
            var s = seq.Statements[i];
            var simple = s switch
            {
                SkipStmt sk => NonTerminal("SkipStmt", sk.Span, Terminal("KEYWORD", "skip", sk.Span)),
                AssignStmt a => NonTerminal("AssignStmt", a.Span,
                    Terminal("IDENT", a.VariableName, a.VariableSpan),
                    Terminal("OP", ":=", a.VariableSpan),
                    BuildArithExpr(a.Expression)),
                IfStmt ifS => BuildIf(ifS),
                WhileStmt w => NonTerminal("WhileStmt", w.Span,
                    Terminal("KEYWORD", "while", w.Span),
                    BuildBoolExpr(w.Condition),
                    Terminal("KEYWORD", "do", w.Span),
                    BuildStatement(w.Body)),
                BlockStmt b => NonTerminal("BlockStmt", b.Span,
                    Terminal("KEYWORD", "begin", b.Span),
                    BuildStatement(b.InnerStatement),
                    Terminal("KEYWORD", "end", b.Span)),
                _ => BuildStatement(s)
            };

            children.Add(NonTerminal("SimpleStatement", s.Span, simple));
            if (i < seq.Statements.Count - 1)
                children.Add(Terminal("SEP", ";", s.Span));
        }

        return NonTerminal("Sequence", seq.Span, [.. children]);
    }

    private static ParseTreeNode BuildIf(IfStmt i)
    {
        var children = new List<ParseTreeNode>
        {
            Terminal("KEYWORD", "if", i.Span),
            BuildBoolExpr(i.Condition),
            Terminal("KEYWORD", "then", i.Span),
            BuildStatement(i.ThenBranch)
        };

        if (i.ElseBranch is not null)
        {
            children.Add(Terminal("KEYWORD", "else", i.Span));
            children.Add(BuildStatement(i.ElseBranch));
        }

        return NonTerminal("IfStmt", i.Span, [.. children]);
    }

    private static ParseTreeNode BuildArithExpr(ArithExpr expr)
    {
        if (expr is BinaryArithExpr { Operator: BinaryArithOperator.Add or BinaryArithOperator.Subtract } bin)
        {
            string op = bin.Operator == BinaryArithOperator.Add ? "+" : "-";
            return NonTerminal("ArithExpr", expr.Span,
                BuildArithExpr(bin.Left),
                Terminal("OP", op, bin.Span),
                BuildTerm(bin.Right));
        }

        return NonTerminal("ArithExpr", expr.Span, BuildTerm(expr));
    }

    private static ParseTreeNode BuildTerm(ArithExpr expr)
    {
        if (expr is BinaryArithExpr { Operator: BinaryArithOperator.Multiply or BinaryArithOperator.Divide or BinaryArithOperator.Modulo } bin)
        {
            string op = bin.Operator switch
            {
                BinaryArithOperator.Multiply => "*",
                BinaryArithOperator.Divide => "/",
                _ => "%"
            };
            return NonTerminal("Term", expr.Span,
                BuildTerm(bin.Left),
                Terminal("OP", op, bin.Span),
                BuildFactor(bin.Right));
        }

        return NonTerminal("Term", expr.Span, BuildFactor(expr));
    }

    private static ParseTreeNode BuildFactor(ArithExpr expr)
    {
        if (expr is UnaryArithExpr u)
        {
            return NonTerminal("Factor", expr.Span,
                Terminal("OP", "-", u.Span),
                BuildFactor(u.Operand));
        }

        return NonTerminal("Factor", expr.Span, BuildPrimary(expr));
    }

    private static ParseTreeNode BuildPrimary(ArithExpr expr) => expr switch
    {
        NumberExpr n => NonTerminal("Primary", n.Span, Terminal("NUMBER", n.Value.ToString(), n.Span)),
        VariableExpr v => NonTerminal("Primary", v.Span, Terminal("IDENT", v.Name, v.Span)),
        _ => NonTerminal("Primary", expr.Span,
            Terminal("LPAREN", "(", expr.Span),
            BuildArithExpr(expr),
            Terminal("RPAREN", ")", expr.Span))
    };

    private static ParseTreeNode BuildBoolExpr(BoolExpr expr)
    {
        if (expr is LogicalExpr { Operator: LogicalOperator.Or } orExpr)
        {
            return NonTerminal("BoolExpr", expr.Span,
                BuildBoolExpr(orExpr.Left),
                Terminal("KEYWORD", "or", orExpr.Span),
                BuildBoolTerm(orExpr.Right));
        }

        return NonTerminal("BoolExpr", expr.Span, BuildBoolTerm(expr));
    }

    private static ParseTreeNode BuildBoolTerm(BoolExpr expr)
    {
        if (expr is LogicalExpr { Operator: LogicalOperator.And } andExpr)
        {
            return NonTerminal("BoolTerm", expr.Span,
                BuildBoolTerm(andExpr.Left),
                Terminal("KEYWORD", "and", andExpr.Span),
                BuildBoolFactor(andExpr.Right));
        }

        return NonTerminal("BoolTerm", expr.Span, BuildBoolFactor(expr));
    }

    private static ParseTreeNode BuildBoolFactor(BoolExpr expr) => expr switch
    {
        NotExpr n => NonTerminal("BoolFactor", n.Span,
            Terminal("KEYWORD", "not", n.Span),
            BuildBoolFactor(n.Operand)),
        BoolLiteralExpr bl => NonTerminal("BoolFactor", bl.Span,
            Terminal("BOOL", bl.Value.ToString().ToLowerInvariant(), bl.Span)),
        ComparisonExpr cmp => NonTerminal("BoolFactor", cmp.Span,
            NonTerminal("Comparison", cmp.Span,
                BuildArithExpr(cmp.Left),
                Terminal("RELOP", FormatRelOp(cmp.Operator), cmp.Span),
                BuildArithExpr(cmp.Right))),
        _ => NonTerminal("BoolFactor", expr.Span,
            Terminal("LPAREN", "(", expr.Span),
            BuildBoolExpr(expr),
            Terminal("RPAREN", ")", expr.Span))
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

    private static ParseTreeNode NonTerminal(string symbol, SourceSpan span, params ParseTreeNode[] children) =>
        new(symbol, IsTerminal: false, span, children);

    private static ParseTreeNode Terminal(string symbol, string lexeme, SourceSpan span) =>
        new(symbol, IsTerminal: true, span, [], lexeme);
}