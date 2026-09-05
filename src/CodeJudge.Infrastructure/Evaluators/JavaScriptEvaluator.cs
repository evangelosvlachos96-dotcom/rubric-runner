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
/// JavaScript evaluator (System Design §6.5). Parses with <c>node --check</c> and runs the
/// submission against the problem's cases via <c>harness.js</c>, comparing results with
/// <see cref="JsonValueComparer"/>. Mirrors <see cref="PythonEvaluator"/>.
/// </summary>
public sealed partial class JavaScriptEvaluator : ICodeEvaluator
{
    private const string Executable = "node";
    private static readonly string Harness = HarnessResources.Load("harness.js");

    private readonly ProcessRunner _processRunner;
    private readonly EvaluationSettings _settings;

    public JavaScriptEvaluator(ProcessRunner processRunner, IOptions<EvaluationSettings> settings)
    {
        _processRunner = processRunner;
        _settings = settings.Value;
    }

    public Language Language => Language.JavaScript;

    public async Task<CompileResult> CompileAsync(string code, CancellationToken cancellationToken)
    {
        // `node --check` needs a file; it parses without executing.
        var scriptPath = NewScriptPath();
        await File.WriteAllTextAsync(scriptPath, code, cancellationToken);

        try
        {
            var result = await _processRunner.RunAsync(
                Executable,
                ["--check", scriptPath],
                stdin: null,
                TimeSpan.FromSeconds(_settings.CompileTimeoutSeconds),
                cancellationToken);

            if (result.ExitCode == 0 && !result.TimedOut)
            {
                return CompileResult.Ok(result.DurationMs);
            }

            return CompileResult.Failure([ParseSyntaxError(result.Stderr, scriptPath)], result.DurationMs);
        }
        finally
        {
            TryDelete(scriptPath);
        }
    }

    public async Task<HarnessRunResult> RunAsync(string code, ProblemDefinition problem, CancellationToken cancellationToken)
    {
        var functionName = problem.Signatures[Language].FunctionName;
        var input = JsonSerializer.Serialize(new
        {
            function = functionName,
            cases = problem.Cases.Select(c => new { id = c.Id, args = c.Args }),
        });

        var scriptPath = NewScriptPath();
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

    private static string NewScriptPath()
        => Path.Combine(Path.GetTempPath(), $"cj_{Guid.CreateVersion7():N}.js");

    // node --check prints e.g.:
    //   C:\...\cj_abc.js:2
    //   function sumTwo(a, b) { return a + b
    //                                        ^
    //   SyntaxError: Unexpected end of input
    private static CompileDiagnostic ParseSyntaxError(string stderr, string scriptPath)
    {
        var line = 0;
        var lineMatch = LineNumberRegex().Match(stderr);
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var parsed))
        {
            line = parsed;
        }

        var message = stderr
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.Contains("Error", StringComparison.Ordinal))
            ?? "Parse error.";

        // Never echo the temp path back to the client.
        message = message.Replace(scriptPath, "submission.js", StringComparison.OrdinalIgnoreCase);

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

    [GeneratedRegex(@"\.js:(\d+)")]
    private static partial Regex LineNumberRegex();
}
