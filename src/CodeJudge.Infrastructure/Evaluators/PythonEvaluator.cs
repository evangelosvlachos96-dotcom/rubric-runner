using System.Text.Json;
using System.Text.RegularExpressions;
using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;
using CodeJudge.Infrastructure.Workers;
using Microsoft.Extensions.Options;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>
/// Python evaluator (System Design §6.5). Parses with <c>python -c "ast.parse(...)"</c> and runs the
/// submission against the problem's cases via <c>harness.py</c>, comparing results with
/// <see cref="JsonValueComparer"/>.
/// </summary>
public sealed partial class PythonEvaluator : ICodeEvaluator
{
    private static readonly string Executable = OperatingSystem.IsWindows() ? "python" : "python3";
    private static readonly string Harness = HarnessResources.Load("harness.py");

    private readonly ProcessRunner _processRunner;
    private readonly EvaluationSettings _settings;

    public PythonEvaluator(ProcessRunner processRunner, IOptions<EvaluationSettings> settings)
    {
        _processRunner = processRunner;
        _settings = settings.Value;
    }

    public Language Language => Language.Python;

    public async Task<CompileResult> CompileAsync(string code, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync(
            Executable,
            ["-c", "import ast,sys; ast.parse(sys.stdin.read())"],
            code,
            TimeSpan.FromSeconds(_settings.CompileTimeoutSeconds),
            cancellationToken);

        if (result.ExitCode == 0 && !result.TimedOut)
        {
            return CompileResult.Ok(result.DurationMs);
        }

        return CompileResult.Failure([ParseSyntaxError(result.Stderr)], result.DurationMs);
    }

    public async Task<HarnessRunResult> RunAsync(string code, ProblemDefinition problem, CancellationToken cancellationToken)
    {
        var functionName = problem.Signatures[Language].FunctionName;
        var input = JsonSerializer.Serialize(new
        {
            function = functionName,
            cases = problem.Cases.Select(c => new { id = c.Id, args = c.Args }),
        });

        var scriptPath = Path.Combine(Path.GetTempPath(), $"cj_{Guid.CreateVersion7():N}.py");
        await File.WriteAllTextAsync(scriptPath, code + "\n\n" + Harness, cancellationToken);

        try
        {
            var result = await _processRunner.RunAsync(
                Executable,
                [scriptPath],
                input,
                TimeSpan.FromSeconds(_settings.RunTimeoutSeconds),
                cancellationToken);

            if (result.TimedOut)
            {
                return new HarnessRunResult([], TimedOut: true, Stderr: null, result.DurationMs);
            }

            return HarnessProtocol.Parse(result.Stdout, result.Stderr, result.DurationMs, problem);
        }
        finally
        {
            TryDelete(scriptPath);
        }
    }

    private static CompileDiagnostic ParseSyntaxError(string stderr)
    {
        var line = 0;
        var lineMatch = LineNumberRegex().Match(stderr);
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var parsed))
        {
            line = parsed;
        }

        var message = stderr
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(l => l.Contains("Error", StringComparison.Ordinal)) ?? "Parse error.";

        return new CompileDiagnostic(line, 0, "SyntaxError", message);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp harness file.
        }
    }

    [GeneratedRegex(@"line (\d+)")]
    private static partial Regex LineNumberRegex();
}
