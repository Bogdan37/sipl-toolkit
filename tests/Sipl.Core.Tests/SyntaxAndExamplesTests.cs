using Sipl.Core.Syntax;
using Sipl.Core.Visualization;

namespace Sipl.Core.Tests;

public class SyntaxAndExamplesTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "examples")) &&
                Directory.Exists(Path.Combine(dir.FullName, "grammar")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Не вдалося знайти корінь репозиторію з папками examples та grammar.");
    }

    [Fact]
    public void AllValidExamples_ShouldParseSuccessfully()
    {
        string validDir = Path.Combine(FindRepoRoot(), "examples", "valid");
        var files = Directory.GetFiles(validDir, "*.sipl");

        Assert.True(files.Length >= 10, $"Очікувалося >= 10 валідних прикладів, знайдено {files.Length}.");

        foreach (var file in files)
        {
            string source = File.ReadAllText(file);
            var result = Parser.ParseSource(source);

            Assert.True(result.IsSuccess,
                $"Файл '{Path.GetFileName(file)}' мав розпарситися успішно, але отримано помилки: " +
                string.Join("; ", result.Diagnostics));
            Assert.NotNull(result.Program);
        }
    }

    [Fact]
    public void AllInvalidExamples_ShouldFailWithDiagnostics()
    {
        string invalidDir = Path.Combine(FindRepoRoot(), "examples", "invalid");
        var files = Directory.GetFiles(invalidDir, "*.sipl");

        Assert.True(files.Length >= 10, $"Очікувалося >= 10 невалідних прикладів, знайдено {files.Length}.");

        foreach (var file in files)
        {
            string source = File.ReadAllText(file);
            var result = Parser.ParseSource(source);

            Assert.False(result.IsSuccess,
                $"Файл '{Path.GetFileName(file)}' мав завершитися синтаксичною або лексичною помилкою.");
            Assert.NotEmpty(result.Diagnostics);
            Assert.All(result.Diagnostics, d => Assert.True(d.Span.Start.Line >= 1 && d.Span.Start.Column >= 1));
        }
    }

    [Fact]
    public void Parser_RespectsOperatorPrecedence()
    {
        var result = Parser.ParseSource("R := 2 + 3 * 4 - 10 / 2");
        Assert.True(result.IsSuccess);

        var assign = Assert.IsType<AssignStmt>(result.Program!.Body);
        var rootSub = Assert.IsType<BinaryArithExpr>(assign.Expression);
        Assert.Equal(BinaryArithOperator.Subtract, rootSub.Operator);

        var leftAdd = Assert.IsType<BinaryArithExpr>(rootSub.Left);
        Assert.Equal(BinaryArithOperator.Add, leftAdd.Operator);

        var mul = Assert.IsType<BinaryArithExpr>(leftAdd.Right);
        Assert.Equal(BinaryArithOperator.Multiply, mul.Operator);

        var div = Assert.IsType<BinaryArithExpr>(rootSub.Right);
        Assert.Equal(BinaryArithOperator.Divide, div.Operator);
    }

    [Fact]
    public void AstVisualizer_GeneratesTextDotAndJson()
    {
        var result = Parser.ParseSource("begin if X > 0 then Y := 1 else Y := -1 end");
        Assert.True(result.IsSuccess);

        string text = AstVisualizer.ToTextTree(result.Program!);
        string dot = AstVisualizer.ToDot(result.Program!);
        string json = AstVisualizer.ToJson(result.Program!);

        Assert.Contains("Program", text);
        Assert.Contains("If-Then-Else", text);
        Assert.StartsWith("digraph SiplAst", dot);
        Assert.Contains("\"type\": \"ProgramNode\"", json);
    }
}