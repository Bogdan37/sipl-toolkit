namespace Sipl.Core.Execution;

public sealed record ExecutionOptions
{
    public int MaxSteps { get; init; } = 10_000;
    public bool RecordTrace { get; init; } = true;
    public bool DefaultUninitializedToZero { get; init; } = true;
    public IReadOnlyDictionary<string, int>? InitialState { get; init; }
}