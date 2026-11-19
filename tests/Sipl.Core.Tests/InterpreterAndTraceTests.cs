using Sipl.Core.Execution;
using Sipl.Core.Syntax;

namespace Sipl.Core.Tests;

public class InterpreterAndTraceTests
{
    [Theory]
    [InlineData(2, 5, 32)]
    [InlineData(3, 0, 1)]
    [InlineData(5, 3, 125)]
    public void Execute_PowerProgram_ComputesExpectedResult(int x, int y, int expectedR)
    {
        const string code = "begin R := 1; while Y > 0 do begin R := R * X; Y := Y - 1 end end";
        var program = Parser.ParseSource(code).Program!;

        var interpreter = new Interpreter(new ExecutionOptions
        {
            InitialState = new Dictionary<string, int> { ["X"] = x, ["Y"] = y }
        });

        var result = interpreter.Execute(program);

        Assert.Equal(ExecutionStatus.Completed, result.Status);
        Assert.Equal(expectedR, result.FinalState["R"]);
        Assert.Equal(0, result.FinalState["Y"]);
    }

    [Fact]
    public void Execute_GcdProgram_ComputesGreatestCommonDivisor()
    {
        const string code = "while A <> B do if A > B then A := A - B else B := B - A";
        var program = Parser.ParseSource(code).Program!;

        var interpreter = new Interpreter(new ExecutionOptions
        {
            InitialState = new Dictionary<string, int> { ["A"] = 48, ["B"] = 18 }
        });

        var result = interpreter.Execute(program);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.FinalState["A"]);
        Assert.Equal(6, result.FinalState["B"]);
    }

    [Fact]
    public void Execute_InfiniteLoop_StopsAtMaxSteps()
    {
        const string code = "while true do skip";
        var program = Parser.ParseSource(code).Program!;

        var interpreter = new Interpreter(new ExecutionOptions { MaxSteps = 50 });
        var result = interpreter.Execute(program);

        Assert.Equal(ExecutionStatus.StepLimitExceeded, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Code == "RUN001");
    }

    [Fact]
    public void Execute_DivisionByZero_ReturnsRuntimeError()
    {
        const string code = "X := 10 / (5 - 5)";
        var program = Parser.ParseSource(code).Program!;

        var result = new Interpreter().Execute(program);

        Assert.Equal(ExecutionStatus.RuntimeError, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Code == "RUN003");
    }

    [Fact]
    public void Execute_StrictUninitializedVariable_ReturnsRuntimeError()
    {
        const string code = "X := Y + 1";
        var program = Parser.ParseSource(code).Program!;

        var interpreter = new Interpreter(new ExecutionOptions { DefaultUninitializedToZero = false });
        var result = interpreter.Execute(program);

        Assert.Equal(ExecutionStatus.RuntimeError, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Code == "RUN002");
    }

    [Fact]
    public void Execute_RecordsAllTraceEventKinds()
    {
        const string code = "begin X := 1; if X = 1 then X := X + 2 else skip end";
        var program = Parser.ParseSource(code).Program!;

        var result = new Interpreter(new ExecutionOptions { RecordTrace = true }).Execute(program);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.BeforeStatement);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.AfterStatement);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.VariableChanged && e.VariableName == "X" && e.NewValue == 3);
        Assert.Contains(result.Trace, e => e.Kind == TraceEventKind.ConditionChecked && e.ConditionResult == true);
        Assert.Equal(TraceEventKind.Finished, result.Trace[^1].Kind);
    }
}