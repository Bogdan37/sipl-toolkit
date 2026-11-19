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
    
    public static string ToComparisonHtml(ProgramNode program, string sourceCode)
    {
        ArgumentNullException.ThrowIfNull(program);
        var cst = ParseTreeBuilder.Build(program);

        int cstNodes = CountParseTreeNodes(cst);
        int astNodes = CountAstNodes(program);

        string cstText = System.Net.WebUtility.HtmlEncode(ParseTreeBuilder.ToTextTree(cst));
        string astText = System.Net.WebUtility.HtmlEncode(ToTextTree(program));
        string escapedSource = System.Net.WebUtility.HtmlEncode(sourceCode);

        return $$"""
            <!DOCTYPE html>
            <html lang="uk">
            <head>
              <meta charset="utf-8" />
              <title>SIPL: Parse Tree (CST) vs AST Comparison</title>
              <style>
                body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; margin: 24px; background: #0f172a; color: #e2e8f0; }
                h1 { font-size: 20px; margin-bottom: 8px; color: #38bdf8; }
                .meta { font-size: 13px; color: #94a3b8; margin-bottom: 16px; }
                .source-box { background: #1e293b; border: 1px solid #334155; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; font-family: Consolas, monospace; white-space: pre-wrap; }
                .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
                .card { background: #1e293b; border: 1px solid #334155; border-radius: 8px; padding: 16px; overflow-x: auto; }
                .card h2 { margin-top: 0; font-size: 16px; display: flex; justify-content: space-between; align-items: center; }
                .badge { background: #0284c7; color: #fff; font-size: 12px; padding: 2px 8px; border-radius: 12px; }
                .badge.ast { background: #059669; }
                pre { margin: 0; font-family: Consolas, "Courier New", monospace; font-size: 13px; line-height: 1.45; }
              </style>
            </head>
            <body>
              <h1>SIPL Syntax Comparison: Concrete Parse Tree vs Abstract Syntax Tree</h1>
              <div class="meta">Згенеровано автоматично модулем VIS-02 (Sipl.Core)</div>
              <div class="source-box">{{escapedSource}}</div>
              <div class="grid">
                <div class="card">
                  <h2>Parse Tree (Конкретний синтаксис) <span class="badge">Вузлів: {{cstNodes}}</span></h2>
                  <pre>{{cstText}}</pre>
                </div>
                <div class="card">
                  <h2>AST (Абстрактний синтаксис) <span class="badge ast">Вузлів: {{astNodes}}</span></h2>
                  <pre>{{astText}}</pre>
                </div>
              </div>
            </body>
            </html>
            """;
    }

    private static int CountParseTreeNodes(ParseTreeNode node) =>
        1 + node.Children.Sum(CountParseTreeNodes);

    private static int CountAstNodes(AstNode node) =>
        1 + GetChildren(node).Sum(CountAstNodes);
}