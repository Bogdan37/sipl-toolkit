using Sipl.Core.Execution;
using Sipl.Core.Semantics;
using Sipl.Core.Syntax;

namespace Sipl.Core.Tests;

public class SemanticsAndCliTests
{
    [Fact]
    public void SemanticTermBuilder_GoldenNotationTests()
    {
        var skipTerm = SemanticTermBuilder.Build(Parser.ParseSource("skip").Program!);
        Assert.Equal("id", skipTerm.ToNotation());

        var seqTerm = SemanticTermBuilder.Build(Parser.ParseSource("X := 1; Y := X + 2").Program!);
        Assert.Equal("(assign(Y, (X + 2)) ∘ assign(X, 1))", seqTerm.ToNotation());

        var whileTerm = SemanticTermBuilder.Build(Parser.ParseSource("while X > 0 do X := X - 1").Program!);
        Assert.Equal("fix(λF. cond((X > 0), F ∘ assign(X, (X - 1)), id))", whileTerm.ToNotation());
    }

    [Fact]
    public void SemanticEvaluator_MatchesInterpreter_OnFactorial()
    {
        const string code = "begin F := 1; I := 1; while I <= N do begin F := F * I; I := I + 1 end end";
        var program = Parser.ParseSource(code).Program!;
        var init = new Dictionary<string, int> { ["N"] = 5 };

        var interpResult = new Interpreter(new ExecutionOptions { InitialState = init }).Execute(program);
        var semTerm = SemanticTermBuilder.Build(program);
        var semResult = new SemanticEvaluator().Evaluate(semTerm, init);

        Assert.True(interpResult.IsSuccess);
        Assert.True(semResult.IsDefined);
        Assert.Equal(120, semResult.FinalState!["F"]);
        Assert.Equal(interpResult.FinalState["F"], semResult.FinalState["F"]);
        Assert.NotEmpty(semResult.Steps);
    }

    [Fact]
    public void SemanticEvaluator_DivergentLoop_ReturnsBottom()
    {
        var program = Parser.ParseSource("while true do X := X + 1").Program!;
        var term = SemanticTermBuilder.Build(program);

        var result = new SemanticEvaluator(maxUnfoldings: 20).Evaluate(term);

        Assert.False(result.IsDefined);
        Assert.Null(result.FinalState);
        Assert.Contains("⊥", result.Steps[^1].Explanation);
    }

    [Fact]
    public void Cli_Integration_CheckRunAndInvalidExitCodes()
    {
        string tempValid = Path.Combine(Path.GetTempPath(), $"sipl_valid_{Guid.NewGuid():N}.sipl");
        string tempInvalid = Path.Combine(Path.GetTempPath(), $"sipl_invalid_{Guid.NewGuid():N}.sipl");

        try
        {
            File.WriteAllText(tempValid, "R := X * 2 + 1");
            File.WriteAllText(tempInvalid, "R = 10");

            int checkValidCode = Sipl.Cli.Program.Main(["check", tempValid, "--json"]);
            int runValidCode = Sipl.Cli.Program.Main(["run", tempValid, "--var", "X=7", "--json"]);
            int checkInvalidCode = Sipl.Cli.Program.Main(["check", tempInvalid, "--json"]);
            int missingFileCode = Sipl.Cli.Program.Main(["check", "non_existent_file.sipl"]);

            Assert.Equal(0, checkValidCode);
            Assert.Equal(0, runValidCode);
            Assert.Equal(2, checkInvalidCode);
            Assert.Equal(1, missingFileCode);
        }
        finally
        {
            if (File.Exists(tempValid)) File.Delete(tempValid);
            if (File.Exists(tempInvalid)) File.Delete(tempInvalid);
        }
    }
}