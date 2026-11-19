using Sipl.Core.Diagnostics;
using Sipl.Core.Syntax;

namespace Sipl.Core.Execution;

public enum ExecutionStatus
{
    Completed,
    StepLimitExceeded,
    RuntimeError
}

public sealed record ExecutionResult(
    ExecutionStatus Status,
    IReadOnlyDictionary<string, int> FinalState,
    int StepsExecuted,
    IReadOnlyList<TraceEvent> Trace,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsSuccess => Status == ExecutionStatus.Completed;
}

public sealed class Interpreter
{
    private readonly ExecutionOptions _options;
    private readonly Dictionary<string, int> _state;
    private readonly List<TraceEvent> _trace = [];
    private readonly List<Diagnostic> _diagnostics = [];
    private int _steps;

    public Interpreter(ExecutionOptions? options = null)
    {
        _options = options ?? new ExecutionOptions();
        _state = _options.InitialState is not null
            ? new Dictionary<string, int>(_options.InitialState, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public ExecutionResult Execute(ProgramNode program)
    {
        ArgumentNullException.ThrowIfNull(program);

        try
        {
            ExecuteStatement(program.Body);

            RecordEvent(
                TraceEventKind.Finished,
                nameof(ProgramNode),
                program.Span,
                description: $"Програму завершено за {_steps} крок(ів).");

            return new ExecutionResult(
                ExecutionStatus.Completed,
                new Dictionary<string, int>(_state),
                _steps,
                _trace,
                _diagnostics);
        }
        catch (StepLimitException ex)
        {
            _diagnostics.Add(new Diagnostic(
                "RUN001",
                $"Перевищено ліміт кроків виконання ({_options.MaxSteps}). Можливий нескінченний цикл.",
                ex.Span));

            return new ExecutionResult(
                ExecutionStatus.StepLimitExceeded,
                new Dictionary<string, int>(_state),
                _steps,
                _trace,
                _diagnostics);
        }
        catch (RuntimeExecutionException ex)
        {
            _diagnostics.Add(new Diagnostic(ex.Code, ex.Message, ex.Span));

            return new ExecutionResult(
                ExecutionStatus.RuntimeError,
                new Dictionary<string, int>(_state),
                _steps,
                _trace,
                _diagnostics);
        }
    }

    private void ExecuteStatement(Stmt stmt)
    {
        if (_steps >= _options.MaxSteps)
            throw new StepLimitException(stmt.Span);

        _steps++;
        string stmtName = stmt.GetType().Name;

        RecordEvent(TraceEventKind.BeforeStatement, stmtName, stmt.Span);

        switch (stmt)
        {
            case SkipStmt:
                break;

            case AssignStmt assign:
            {
                int newValue = EvaluateArith(assign.Expression);
                bool existed = _state.TryGetValue(assign.VariableName, out int oldValue);
                _state[assign.VariableName] = newValue;

                RecordEvent(
                    TraceEventKind.VariableChanged,
                    stmtName,
                    assign.Span,
                    variableName: assign.VariableName,
                    oldValue: existed ? oldValue : null,
                    newValue: newValue,
                    description: $"{assign.VariableName} := {newValue}");
                break;
            }

            case SequenceStmt seq:
            {
                foreach (var child in seq.Statements)
                    ExecuteStatement(child);
                break;
            }

            case BlockStmt block:
            {
                ExecuteStatement(block.InnerStatement);
                break;
            }

            case IfStmt ifStmt:
            {
                bool cond = EvaluateBool(ifStmt.Condition);
                RecordEvent(
                    TraceEventKind.ConditionChecked,
                    stmtName,
                    ifStmt.Condition.Span,
                    conditionResult: cond,
                    description: $"if умова = {cond}");

                if (cond)
                    ExecuteStatement(ifStmt.ThenBranch);
                else if (ifStmt.ElseBranch is not null)
                    ExecuteStatement(ifStmt.ElseBranch);
                break;
            }

            case WhileStmt whileStmt:
            {
                while (true)
                {
                    if (_steps >= _options.MaxSteps)
                        throw new StepLimitException(whileStmt.Span);

                    bool cond = EvaluateBool(whileStmt.Condition);
                    RecordEvent(
                        TraceEventKind.ConditionChecked,
                        stmtName,
                        whileStmt.Condition.Span,
                        conditionResult: cond,
                        description: $"while умова = {cond}");

                    if (!cond)
                        break;

                    ExecuteStatement(whileStmt.Body);
                }
                break;
            }

            default:
                throw new InvalidOperationException($"Непідтримуваний вузол AST: {stmt.GetType().Name}");
        }

        RecordEvent(TraceEventKind.AfterStatement, stmtName, stmt.Span);
    }

    private int EvaluateArith(ArithExpr expr)
    {
        return expr switch
        {
            NumberExpr num => num.Value,

            VariableExpr varExpr => ResolveVariable(varExpr),

            UnaryArithExpr unary => unary.Operator switch
            {
                UnaryArithOperator.Negate => -EvaluateArith(unary.Operand),
                _ => throw new InvalidOperationException()
            },

            BinaryArithExpr bin => EvaluateBinaryArith(bin),

            _ => throw new InvalidOperationException($"Непідтримуваний вираз: {expr.GetType().Name}")
        };
    }

    private int ResolveVariable(VariableExpr varExpr)
    {
        if (_state.TryGetValue(varExpr.Name, out int value))
            return value;

        if (_options.DefaultUninitializedToZero)
            return 0;

        throw new RuntimeExecutionException(
            "RUN002",
            $"Змінна '{varExpr.Name}' використовується до присвоєння їй значення.",
            varExpr.Span);
    }

    private int EvaluateBinaryArith(BinaryArithExpr bin)
    {
        int left = EvaluateArith(bin.Left);
        int right = EvaluateArith(bin.Right);

        return bin.Operator switch
        {
            BinaryArithOperator.Add => left + right,
            BinaryArithOperator.Subtract => left - right,
            BinaryArithOperator.Multiply => left * right,
            BinaryArithOperator.Divide => right == 0
                ? throw new RuntimeExecutionException("RUN003", "Ділення на нуль.", bin.Span)
                : left / right,
            BinaryArithOperator.Modulo => right == 0
                ? throw new RuntimeExecutionException("RUN004", "Остача від ділення на нуль.", bin.Span)
                : left % right,
            _ => throw new InvalidOperationException()
        };
    }

    private bool EvaluateBool(BoolExpr expr)
    {
        return expr switch
        {
            BoolLiteralExpr lit => lit.Value,

            NotExpr not => !EvaluateBool(not.Operand),

            LogicalExpr logic => logic.Operator switch
            {
                LogicalOperator.And => EvaluateBool(logic.Left) && EvaluateBool(logic.Right),
                LogicalOperator.Or => EvaluateBool(logic.Left) || EvaluateBool(logic.Right),
                _ => throw new InvalidOperationException()
            },

            ComparisonExpr cmp => EvaluateComparison(cmp),

            _ => throw new InvalidOperationException($"Непідтримуваний булевий вираз: {expr.GetType().Name}")
        };
    }

    private bool EvaluateComparison(ComparisonExpr cmp)
    {
        int left = EvaluateArith(cmp.Left);
        int right = EvaluateArith(cmp.Right);

        return cmp.Operator switch
        {
            RelOperator.Equal => left == right,
            RelOperator.NotEqual => left != right,
            RelOperator.Less => left < right,
            RelOperator.LessOrEqual => left <= right,
            RelOperator.Greater => left > right,
            RelOperator.GreaterOrEqual => left >= right,
            _ => throw new InvalidOperationException()
        };
    }

    private void RecordEvent(
        TraceEventKind kind,
        string statementType,
        SourceSpan span,
        string? variableName = null,
        int? oldValue = null,
        int? newValue = null,
        bool? conditionResult = null,
        string? description = null)
    {
        if (!_options.RecordTrace)
            return;

        _trace.Add(new TraceEvent(
            Step: _steps,
            Kind: kind,
            StatementType: statementType,
            Span: span,
            StateSnapshot: new Dictionary<string, int>(_state),
            VariableName: variableName,
            OldValue: oldValue,
            NewValue: newValue,
            ConditionResult: conditionResult,
            Description: description));
    }

    private sealed class StepLimitException(SourceSpan span) : Exception
    {
        public SourceSpan Span { get; } = span;
    }

    private sealed class RuntimeExecutionException(string code, string message, SourceSpan span) : Exception(message)
    {
        public string Code { get; } = code;
        public SourceSpan Span { get; } = span;
    }
}