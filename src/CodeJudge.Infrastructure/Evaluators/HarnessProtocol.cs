using System.Text.Json;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>
/// Parses the language-agnostic harness stdout (System Design §6.5) into a <see cref="HarnessRunResult"/>,
/// comparing each case's actual value against the catalog's expected via <see cref="JsonValueComparer"/>.
/// </summary>
internal static class HarnessProtocol
{
    public static HarnessRunResult Parse(string stdout, string stderr, int durationMs, ProblemDefinition problem)
    {
        var expectedById = problem.Cases.ToDictionary(c => c.Id, c => c.Expected);

        JsonElement resultsElement;
        try
        {
            using var document = JsonDocument.Parse(stdout);
            if (!document.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return RuntimeError(stderr, stdout, durationMs);
            }

            resultsElement = results.Clone();
        }
        catch (JsonException)
        {
            return RuntimeError(stderr, stdout, durationMs);
        }

        var cases = new List<TestCaseResult>();
        foreach (var item in resultsElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetInt32();
            var error = item.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null;
            var actual = item.TryGetProperty("actual", out var a) ? a.Clone() : default;
            var caseDuration = item.TryGetProperty("durationMs", out var d) && d.ValueKind == JsonValueKind.Number
                ? d.GetDouble()
                : 0;

            var passed = error is null
                && expectedById.TryGetValue(id, out var expected)
                && JsonValueComparer.DeepEquals(actual, expected);

            cases.Add(new TestCaseResult(
                id,
                expectedById.TryGetValue(id, out var exp) ? exp : default,
                actual,
                passed,
                error,
                caseDuration));
        }

        return new HarnessRunResult(cases, TimedOut: false, Stderr: stderr, durationMs);
    }

    private static HarnessRunResult RuntimeError(string stderr, string stdout, int durationMs)
    {
        var message = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
        return new HarnessRunResult([], TimedOut: false, Stderr: message, durationMs);
    }
}
