using Sipl.Core.Syntax;

namespace Sipl.Core.Semantics;

public sealed record SemanticStep(
    int StepNumber,
    string RuleName,
    string TermNotation,
    string Explanation,
    IReadOnlyDictionary<string, int> StateBefore,
    IReadOnlyDictionary<string, int>? StateAfter);

public sealed record SemanticEvaluationResult(
    bool IsDefined,
    IReadOnlyDictionary<string, int>? FinalState,
    string? UndefinedReason,
    IReadOnlyList<SemanticStep> Steps);

public sealed class SemanticEvaluator
{
    private readonly int _maxUnfoldings;
    private readonly bool _defaultUninitializedToZero;
    private readonly List<SemanticStep> _steps = [];
    private int _stepCounter;

    public SemanticEvaluator(int maxUnfoldings = 1_000, bool defaultUninitializedToZero = true)
    {
        _maxUnfoldings = maxUnfoldings;
        _defaultUninitializedToZero = defaultUninitializedToZero;
    }

    public SemanticEvaluationResult Evaluate(
        SemanticTerm term,
        IReadOnlyDictionary<string, int>? initialState = null)
    {
        ArgumentNullException.ThrowIfNull(term);

        var state = initialState is not null
            ? new Dictionary<string, int>(initialState, StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            var finalState = ApplyTerm(term, state, unfoldingDepth: 0);
            return new SemanticEvaluationResult(
                IsDefined: true,
                FinalState: finalState,
                UndefinedReason: null,
                Steps: _steps);
        }
        catch (UndefinedSemanticsException ex)
        {
            _steps.Add(new SemanticStep(
                ++_stepCounter,
                "Rule-Bottom (⊥)",
                term.ToNotation(),
                $"Семантика невизначена (⊥): {ex.Message}",
                new Dictionary<string, int>(state),
                null));

            return new SemanticEvaluationResult(
                IsDefined: false,
                FinalState: null,
                UndefinedReason: ex.Message,
                Steps: _steps);
        }
    }

    private Dictionary<string, int> ApplyTerm(
        SemanticTerm term,
        Dictionary<string, int> state,
        int unfoldingDepth)
    {
        switch (term)
        {
            case IdentityTerm:
            {
                var snapshot = new Dictionary<string, int>(state);
                _steps.Add(new SemanticStep(
                    ++_stepCounter,
                    "Rule-Skip (id)",
                    term.ToNotation(),
                    $"id({FormatState(snapshot)}) = {FormatState(snapshot)}",
                    snapshot,
                    snapshot));
                return snapshot;
            }

            case AssignTerm assign:
            {
                var before = new Dictionary<string, int>(state);
                int val = EvalArith(assign.Expression, before);
                var after = new Dictionary<string, int>(before)
                {
                    [assign.Variable] = val
                };

                _steps.Add(new SemanticStep(
                    ++_stepCounter,
                    "Rule-Assign",
                    term.ToNotation(),
                    $"Оновлення стану: {assign.Variable} ↦ {val} ⇒ {FormatState(after)}",
                    before,
                    after));
                return after;
            }

            case CompositionTerm comp:
            {
                var before = new Dictionary<string, int>(state);
                _steps.Add(new SemanticStep(
                    ++_stepCounter,
                    "Rule-Comp (∘)",
                    term.ToNotation(),
                    $"Застосування композиції: спочатку {comp.First.ToNotation()}, потім {comp.Second.ToNotation()}",
                    before,
                    before));

                var midState = ApplyTerm(comp.First, before, unfoldingDepth);
                return ApplyTerm(comp.Second, midState, unfoldingDepth);
            }

            case ConditionalTerm cond:
            {
                var before = new Dictionary<string, int>(state);
                bool condVal = EvalBool(cond.Condition, before);
                var chosenBranch = condVal ? cond.ThenTerm : cond.ElseTerm;

                _steps.Add(new SemanticStep(
                    ++_stepCounter,
                    condVal ? "Rule-Cond-True" : "Rule-Cond-False",
                    term.ToNotation(),
                    $"Умова {ExpressionFormatter.FormatBool(cond.Condition)} = {condVal.ToString().ToLowerInvariant()} ⇒ перехід до {chosenBranch.ToNotation()}",
                    before,
                    before));

                return ApplyTerm(chosenBranch, before, unfoldingDepth);
            }

            case FixpointTerm fix:
            {
                var before = new Dictionary<string, int>(state);
                if (unfoldingDepth >= _maxUnfoldings)
                {
                    throw new UndefinedSemanticsException(
                        $"Перевищено глибину розгортання нерухомої точки ({_maxUnfoldings}) — цикл не збігається (результат ⊥).");
                }

                bool condVal = EvalBool(fix.Condition, before);
                if (!condVal)
                {
                    _steps.Add(new SemanticStep(
                        ++_stepCounter,
                        "Rule-Fix-Exit",
                        fix.ToNotation(),
                        $"Умова {ExpressionFormatter.FormatBool(fix.Condition)} = false ⇒ вихід із нерухомої точки (id)",
                        before,
                        before));
                    return before;
                }

                _steps.Add(new SemanticStep(
                    ++_stepCounter,
                    $"Rule-Fix-Unfold (#{unfoldingDepth + 1})",
                    fix.ToNotation(),
                    $"Умова {ExpressionFormatter.FormatBool(fix.Condition)} = true ⇒ розгортання fix: F({fix.BodyTerm.ToNotation()}(s))",
                    before,
                    before));

                var afterBody = ApplyTerm(fix.BodyTerm, before, unfoldingDepth);
                return ApplyTerm(fix, afterBody, unfoldingDepth + 1);
            }

            default:
                throw new InvalidOperationException($"Непідтримуваний семантичний терм: {term.GetType().Name}");
        }
    }

    private int EvalArith(ArithExpr expr, IReadOnlyDictionary<string, int> state) => expr switch
    {
        NumberExpr n => n.Value,
        VariableExpr v => state.TryGetValue(v.Name, out int val)
            ? val
            : (_defaultUninitializedToZero ? 0 : throw new UndefinedSemanticsException($"Невизначена змінна '{v.Name}'.")),
        UnaryArithExpr u => -EvalArith(u.Operand, state),
        BinaryArithExpr b => b.Operator switch
        {
            BinaryArithOperator.Add => EvalArith(b.Left, state) + EvalArith(b.Right, state),
            BinaryArithOperator.Subtract => EvalArith(b.Left, state) - EvalArith(b.Right, state),
            BinaryArithOperator.Multiply => EvalArith(b.Left, state) * EvalArith(b.Right, state),
            BinaryArithOperator.Divide => EvalArith(b.Right, state) == 0
                ? throw new UndefinedSemanticsException("Ділення на нуль.")
                : EvalArith(b.Left, state) / EvalArith(b.Right, state),
            BinaryArithOperator.Modulo => EvalArith(b.Right, state) == 0
                ? throw new UndefinedSemanticsException("Остача від ділення на нуль.")
                : EvalArith(b.Left, state) % EvalArith(b.Right, state),
            _ => throw new InvalidOperationException()
        },
        _ => throw new InvalidOperationException()
    };

    private bool EvalBool(BoolExpr expr, IReadOnlyDictionary<string, int> state) => expr switch
    {
        BoolLiteralExpr bl => bl.Value,
        NotExpr n => !EvalBool(n.Operand, state),
        LogicalExpr l => l.Operator switch
        {
            LogicalOperator.And => EvalBool(l.Left, state) && EvalBool(l.Right, state),
            LogicalOperator.Or => EvalBool(l.Left, state) || EvalBool(l.Right, state),
            _ => throw new InvalidOperationException()
        },
        ComparisonExpr c => c.Operator switch
        {
            RelOperator.Equal => EvalArith(c.Left, state) == EvalArith(c.Right, state),
            RelOperator.NotEqual => EvalArith(c.Left, state) != EvalArith(c.Right, state),
            RelOperator.Less => EvalArith(c.Left, state) < EvalArith(c.Right, state),
            RelOperator.LessOrEqual => EvalArith(c.Left, state) <= EvalArith(c.Right, state),
            RelOperator.Greater => EvalArith(c.Left, state) > EvalArith(c.Right, state),
            RelOperator.GreaterOrEqual => EvalArith(c.Left, state) >= EvalArith(c.Right, state),
            _ => throw new InvalidOperationException()
        },
        _ => throw new InvalidOperationException()
    };

    private static string FormatState(IReadOnlyDictionary<string, int> state)
    {
        if (state.Count == 0)
            return "∅";
        var pairs = state.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}");
        return "{" + string.Join(", ", pairs) + "}";
    }

    private sealed class UndefinedSemanticsException(string message) : Exception(message);
}