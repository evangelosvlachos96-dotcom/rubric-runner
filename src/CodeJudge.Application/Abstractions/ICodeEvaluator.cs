using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Abstractions;

/// <summary>
/// Strategy for one <see cref="Language"/>: knows how to compile/parse and how to run a
/// submission's code against a problem's test cases. One implementation per language,
/// resolved from DI by <see cref="Language"/> (System Design §6.5). Evaluators own the
/// compile and run mechanics only; <see cref="EvaluationService"/> owns the rubric order.
/// </summary>
public interface ICodeEvaluator
{
    Language Language { get; }

    Task<CompileResult> CompileAsync(string code, CancellationToken cancellationToken);

    Task<HarnessRunResult> RunAsync(string code, ProblemDefinition problem, CancellationToken cancellationToken);
}
