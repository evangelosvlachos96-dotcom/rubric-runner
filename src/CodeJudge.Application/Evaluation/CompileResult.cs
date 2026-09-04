namespace CodeJudge.Application.Evaluation;

/// <summary>One compiler/parser diagnostic. Serialised into the <c>Compiles</c> result's output JSON.</summary>
public sealed record CompileDiagnostic(int Line, int Column, string Code, string Message);

/// <summary>Outcome of the compile/parse step for a submission.</summary>
public sealed record CompileResult(
    bool Success,
    IReadOnlyList<CompileDiagnostic> Diagnostics,
    int DurationMs)
{
    public static CompileResult Ok(int durationMs) => new(true, [], durationMs);

    public static CompileResult Failure(IReadOnlyList<CompileDiagnostic> diagnostics, int durationMs)
        => new(false, diagnostics, durationMs);
}
