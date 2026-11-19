using System.Text.Json.Serialization;
using Sipl.Core.Diagnostics;

namespace Sipl.Core.Execution;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TraceEventKind
{
    BeforeStatement,
    AfterStatement,
    VariableChanged,
    ConditionChecked,
    Finished
}

public sealed record TraceEvent(
    int Step,
    TraceEventKind Kind,
    string StatementType,
    SourceSpan Span,
    IReadOnlyDictionary<string, int> StateSnapshot,
    string? VariableName = null,
    int? OldValue = null,
    int? NewValue = null,
    bool? ConditionResult = null,
    string? Description = null);