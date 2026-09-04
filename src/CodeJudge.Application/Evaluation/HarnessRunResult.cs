using System.Text.Json;

namespace CodeJudge.Application.Evaluation;

/// <summary>
/// Result of running a single test case through a language harness. <see cref="Passed"/> is
/// computed by the evaluator by JSON-deep-comparing <see cref="Actual"/> against the catalog's
/// <see cref="Expected"/> (System Design §6.5).
/// </summary>
public sealed record TestCaseResult(
    int Id,
    JsonElement Expected,
    JsonElement? Actual,
    bool Passed,
    string? Error,
    double DurationMs);

/// <summary>
/// Result of one harness invocation, which runs all of a problem's test cases in a single process.
/// </summary>
public sealed record HarnessRunResult(
    IReadOnlyList<TestCaseResult> Cases,
    bool TimedOut,
    string? Stderr,
    int DurationMs);
