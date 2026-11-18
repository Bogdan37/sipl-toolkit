namespace Sipl.Core.Diagnostics;

public readonly record struct SourcePosition(int Line, int Column, int Offset)
{
    public static readonly SourcePosition Start = new(1, 1, 0);

    public override string ToString() => $"{Line}:{Column}";
}

public readonly record struct SourceSpan(SourcePosition Start, SourcePosition End)
{
    public static SourceSpan Combine(SourceSpan first, SourceSpan second) =>
        new(first.Start, second.End);

    public override string ToString() => $"{Start}-{End}";
}