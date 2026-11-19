using System.Text;
using System.Text.Json;
using Sipl.Core.Syntax;

namespace Sipl.Core.Visualization;

public static class AstVisualizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToTextTree(AstNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var sb = new StringBuilder();
        WriteTextNode(root, sb, string.Empty, isLast: true);
        return sb.ToString();
    }

    public static string ToDot(AstNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var sb = new StringBuilder();
        sb.AppendLine("digraph SiplAst {");
        sb.AppendLine("  node [shape=box, style=\"rounded,filled\", fillcolor=\"#F4F7FA\", fontname=\"Consolas\", fontsize=10];");
        sb.AppendLine("  edge [color=\"#5C6BC0\"];");

        int nextId = 0;
        WriteDotNode(root, sb, ref nextId);

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string ToJson(AstNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var dto = BuildJsonDto(root);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static void WriteTextNode(AstNode node, StringBuilder sb, string indent, bool isLast)
    {
        string connector = isLast ? "└── " : "├── ";
        sb.Append(indent);
        sb.Append(connector);
        sb.AppendLine($"{GetNodeLabel(node)} [{node.Span}]");

        var children = GetChildren(node);
        string childIndent = indent + (isLast ? "    " : "│   ");

        for (int i = 0; i < children.Count; i++)
        {
            WriteTextNode(children[i], sb, childIndent, i == children.Count - 1);
        }
    }

    private static int WriteDotNode(AstNode node, StringBuilder sb, ref int nextId)
    {
        int currentId = nextId++;
        string label = EscapeDot(GetNodeLabel(node) + $"\\n[{node.Span}]");
        sb.AppendLine($"  n{currentId} [label=\"{label}\"];");

        foreach (var child in GetChildren(node))
        {
            int childId = WriteDotNode(child, sb, ref nextId);
            sb.AppendLine($"  n{currentId} -> n{childId};");
        }

        return currentId;
    }

    private static object BuildJsonDto(AstNode node)
    {
        return new
        {
            type = node.GetType().Name,
            label = GetNodeLabel(node),
            span = new
            {
                start = new { line = node.Span.Start.Line, column = node.Span.Start.Column, offset = node.Span.Start.Offset },
                end = new { line = node.Span.End.Line, column = node.Span.End.Column, offset = node.Span.End.Offset }
            },
            children = GetChildren(node).Select(BuildJsonDto).ToList()
        };
    }

    private static string GetNodeLabel(AstNode node) => node switch
    {
        ProgramNode => "Program",
        SkipStmt => "Skip",
        AssignStmt a => $"Assign ({a.VariableName} :=)",
        SequenceStmt s => $"Sequence ({s.Statements.Count})",
        IfStmt i => i.ElseBranch is not null ? "If-Then-Else" : "If-Then",
        WhileStmt => "While-Do",
        BlockStmt => "Block (begin..end)",
        NumberExpr n => $"Number ({n.Value})",
        VariableExpr v => $"Var ({v.Name})",
        UnaryArithExpr u => $"Unary ({u.Operator})",
        BinaryArithExpr b => $"Binary ({b.Operator})",
        BoolLiteralExpr bl => $"Bool ({bl.Value.ToString().ToLowerInvariant()})",
        ComparisonExpr c => $"Compare ({c.Operator})",
        NotExpr => "Not",
        LogicalExpr l => $"Logic ({l.Operator})",
        _ => node.GetType().Name
    };

    private static IReadOnlyList<AstNode> GetChildren(AstNode node) => node switch
    {
        ProgramNode p => [p.Body],
        SkipStmt => [],
        AssignStmt a => [a.Expression],
        SequenceStmt s => s.Statements,
        IfStmt i => i.ElseBranch is not null ? [i.Condition, i.ThenBranch, i.ElseBranch] : [i.Condition, i.ThenBranch],
        WhileStmt w => [w.Condition, w.Body],
        BlockStmt b => [b.InnerStatement],
        NumberExpr => [],
        VariableExpr => [],
        UnaryArithExpr u => [u.Operand],
        BinaryArithExpr b => [b.Left, b.Right],
        BoolLiteralExpr => [],
        ComparisonExpr c => [c.Left, c.Right],
        NotExpr n => [n.Operand],
        LogicalExpr l => [l.Left, l.Right],
        _ => []
    };

    private static string EscapeDot(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}