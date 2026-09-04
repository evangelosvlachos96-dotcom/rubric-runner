using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>
/// JavaScript evaluator.
/// TODO(phase-2): implement <c>node --check</c> compile and <c>node harness.js</c> execution
/// (the embedded <c>harness.js</c> and <see cref="HarnessProtocol"/> are already in place — this
/// mirrors <see cref="PythonEvaluator"/>). For now it reports execution as not implemented so the
/// language stays registered and the rubric produces its three results.
/// </summary>
public sealed class JavaScriptEvaluator : ICodeEvaluator
{
    public Language Language => Language.JavaScript;

    public Task<CompileResult> CompileAsync(string code, CancellationToken cancellationToken)
        => Task.FromResult(CompileResult.Ok(0));

    public Task<HarnessRunResult> RunAsync(string code, ProblemDefinition problem, CancellationToken cancellationToken)
        => Task.FromResult(new HarnessRunResult(
            [],
            TimedOut: false,
            Stderr: "JavaScript execution not implemented yet (phase-2).",
            DurationMs: 0));
}
