namespace Sipl.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

public sealed record Diagnostic(
    string Code,
    string Message,
    SourceSpan Span,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error)
{
    public override string ToString() =>
        $"[{Severity}] {Code} ({Span.Start}): {Message}";
}