using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>
/// C# evaluator.
/// TODO(phase-2): implement Roslyn <c>CSharpCompilation</c> for compile diagnostics and execution via a
/// collectible <c>AssemblyLoadContext</c> + reflection with <c>Task.WaitAsync(timeout)</c>
/// (System Design §6.5). For now it reports execution as not implemented so the language stays
/// registered and the rubric produces its three results.
/// </summary>
public sealed class CSharpEvaluator : ICodeEvaluator
{
    public Language Language => Language.CSharp;

    public Task<CompileResult> CompileAsync(string code, CancellationToken cancellationToken)
        => Task.FromResult(CompileResult.Ok(0));

    public Task<HarnessRunResult> RunAsync(string code, ProblemDefinition problem, CancellationToken cancellationToken)
        => Task.FromResult(new HarnessRunResult(
            [],
            TimedOut: false,
            Stderr: "C# execution not implemented yet (phase-2).",
            DurationMs: 0));
}
