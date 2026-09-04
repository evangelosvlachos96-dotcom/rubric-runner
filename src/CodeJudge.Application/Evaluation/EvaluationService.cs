using System.Text.Json;
using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Common;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Entities;
using CodeJudge.Domain.Enums;
using CodeJudge.Domain.Exceptions;

namespace CodeJudge.Application.Evaluation;

/// <summary>
/// Runs the rubric for a claimed submission in the fixed order Security → Compiles → Test,
/// short-circuiting on the first failure and always producing exactly three
/// <see cref="EvaluationResult"/>s (skipped items flagged). Owns the rubric order and the
/// skip semantics; the language evaluators only compile and run (System Design §6.5, Database Design §9).
/// </summary>
public sealed class EvaluationService
{
    private const int MaxMessageLength = 2000;
    private const int MaxOutputLength = 16 * 1024;

    private readonly IProblemCatalog _catalog;
    private readonly IRestrictedKeywordPolicy _keywordPolicy;
    private readonly IReadOnlyDictionary<Language, ICodeEvaluator> _evaluators;

    public EvaluationService(
        IProblemCatalog catalog,
        IRestrictedKeywordPolicy keywordPolicy,
        IEnumerable<ICodeEvaluator> evaluators)
    {
        _catalog = catalog;
        _keywordPolicy = keywordPolicy;
        _evaluators = evaluators.ToDictionary(e => e.Language);
    }

    /// <summary>
    /// Evaluates the submission and returns the three rubric results. Throws only on conditions
    /// the caller should treat as a system error (missing problem or evaluator); a wrong,
    /// non-compiling, hanging or crashing submission produces failed results, not an exception.
    /// </summary>
    public async Task<IReadOnlyList<EvaluationResult>> EvaluateAsync(
        Submission submission,
        CancellationToken cancellationToken)
    {
        var problem = _catalog.Find(submission.ProblemId)
            ?? throw new NotFoundException("Problem", submission.ProblemId);

        if (!_evaluators.TryGetValue(submission.Language, out var evaluator))
        {
            throw new UnsupportedLanguageException(
                $"No evaluator is registered for language '{submission.Language}'.");
        }

        // 1. Security — applied first, so restricted code is never compiled or executed.
        var match = _keywordPolicy.Check(submission.Language, submission.Code);
        if (match is not null)
        {
            return
            [
                EvaluationResult.Fail(
                    RubricItem.Security,
                    $"Restricted keyword '{match.Keyword}' found on line {match.LineNumber}."),
                EvaluationResult.Skip(RubricItem.Compiles),
                EvaluationResult.Skip(RubricItem.Test),
            ];
        }

        // 2. Compile / parse.
        var compile = await evaluator.CompileAsync(submission.Code, cancellationToken);
        if (!compile.Success)
        {
            var firstMessage = compile.Diagnostics.Count > 0
                ? compile.Diagnostics[0].Message
                : "Compilation failed.";

            return
            [
                EvaluationResult.Pass(RubricItem.Security),
                EvaluationResult.Fail(
                    RubricItem.Compiles,
                    Truncate(firstMessage, MaxMessageLength),
                    SerializeDiagnostics(compile.Diagnostics),
                    durationMs: compile.DurationMs),
                EvaluationResult.Skip(RubricItem.Test),
            ];
        }

        // 3. Test — all catalog cases in one harness run.
        var run = await evaluator.RunAsync(submission.Code, problem, cancellationToken);

        return
        [
            EvaluationResult.Pass(RubricItem.Security),
            EvaluationResult.Pass(RubricItem.Compiles, "Compiled successfully.", durationMs: compile.DurationMs),
            BuildTestResult(problem, run),
        ];
    }

    private static EvaluationResult BuildTestResult(ProblemDefinition problem, HarnessRunResult run)
    {
        var total = problem.Cases.Count;

        if (run.TimedOut)
        {
            return EvaluationResult.Fail(
                RubricItem.Test,
                $"Timed out after {run.DurationMs} ms",
                SerializeCases(problem, run.Cases),
                testsPassed: run.Cases.Count(c => c.Passed),
                testsTotal: total,
                durationMs: run.DurationMs);
        }

        if (run.Cases.Count == 0 && !string.IsNullOrWhiteSpace(run.Stderr))
        {
            // The harness itself failed to run — a runtime crash with no per-case results.
            return EvaluationResult.Fail(
                RubricItem.Test,
                Truncate(run.Stderr!.Trim(), MaxMessageLength),
                testsPassed: 0,
                testsTotal: total,
                durationMs: run.DurationMs);
        }

        var passed = run.Cases.Count(c => c.Passed);
        var allPassed = total > 0 && passed == total && run.Cases.Count == total;
        var message = $"{passed}/{total} test cases passed";
        var output = SerializeCases(problem, run.Cases);

        return allPassed
            ? EvaluationResult.Pass(RubricItem.Test, message, output, passed, total, run.DurationMs)
            : EvaluationResult.Fail(RubricItem.Test, message, output, passed, total, run.DurationMs);
    }

    private static string SerializeDiagnostics(IReadOnlyList<CompileDiagnostic> diagnostics)
        => Cap(JsonSerializer.Serialize(new { diagnostics }, JsonDefaults.Output));

    private static string SerializeCases(ProblemDefinition problem, IReadOnlyList<TestCaseResult> cases)
    {
        var argsById = problem.Cases.ToDictionary(c => c.Id, c => c.Args);
        var shaped = cases.Select(c => new
        {
            id = c.Id,
            args = argsById.TryGetValue(c.Id, out var args) ? args : [],
            expected = c.Expected,
            actual = c.Actual,
            passed = c.Passed,
            error = c.Error,
            durationMs = c.DurationMs,
        });

        return Cap(JsonSerializer.Serialize(new { cases = shaped }, JsonDefaults.Output));
    }

    private static string Cap(string json)
        => json.Length <= MaxOutputLength ? json : "{\"truncated\":true}";

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
