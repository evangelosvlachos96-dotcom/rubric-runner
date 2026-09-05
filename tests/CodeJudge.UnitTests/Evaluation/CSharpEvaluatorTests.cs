using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;
using CodeJudge.Infrastructure.Evaluators;
using CodeJudge.Infrastructure.Workers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeJudge.UnitTests.Evaluation;

/// <summary>
/// The real Roslyn-backed evaluator: no external runtime is needed, so the C# path is covered
/// end to end (compile diagnostics, reflection invoke, timeout) in the unit suite.
/// </summary>
public sealed class CSharpEvaluatorTests
{
    private readonly ProblemCatalog _catalog = new();

    private static CSharpEvaluator Evaluator(int runTimeoutSeconds = 5) =>
        new(Options.Create(new EvaluationSettings { RunTimeoutSeconds = runTimeoutSeconds }));

    private ProblemDefinition Problem(string id) => _catalog.Find(id)!;

    [Fact]
    public async Task Compile_reports_diagnostics_with_user_line_numbers()
    {
        // Line 2 is missing its semicolon; the wrapper must not shift the reported line.
        const string code = "public static int Sum(int a, int b)\n{ return a + b }";

        var result = await Evaluator().CompileAsync(code, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics[0].Code.Should().Be("CS1002");
        result.Diagnostics[0].Line.Should().Be(2);
    }

    [Fact]
    public async Task Full_class_submission_compiles_and_passes_all_cases()
    {
        const string code = "public static class Solution { public static int Sum(int a, int b) => a + b; }";

        var compile = await Evaluator().CompileAsync(code, CancellationToken.None);
        var run = await Evaluator().RunAsync(code, Problem("sum-two-numbers"), CancellationToken.None);

        compile.Success.Should().BeTrue();
        run.TimedOut.Should().BeFalse();
        run.Cases.Should().HaveCount(4);
        run.Cases.Should().OnlyContain(c => c.Passed);
    }

    [Fact]
    public async Task Bare_method_submission_is_wrapped_and_runs()
    {
        const string code = "using System.Linq;\nint[] TwoSum(int[] nums, int target)\n{\n"
                            + "    for (var i = 0; i < nums.Length; i++)\n"
                            + "        for (var j = i + 1; j < nums.Length; j++)\n"
                            + "            if (nums[i] + nums[j] == target) return new[] { i, j };\n"
                            + "    return System.Array.Empty<int>();\n}";

        var compile = await Evaluator().CompileAsync(code, CancellationToken.None);
        var run = await Evaluator().RunAsync(code, Problem("two-sum"), CancellationToken.None);

        compile.Success.Should().BeTrue();
        run.Cases.Should().OnlyContain(c => c.Passed, "expected/actual: " + string.Join("; ", run.Cases.Select(c => $"{c.Expected}/{c.Actual}/{c.Error}")));
    }

    [Fact]
    public async Task Wrong_answer_is_reported_per_case_not_as_an_error()
    {
        const string code = "public static int Sum(int a, int b) => 7;";

        var run = await Evaluator().RunAsync(code, Problem("sum-two-numbers"), CancellationToken.None);

        run.Cases.Should().HaveCount(4);
        run.Cases.Should().OnlyContain(c => c.Error == null);
        run.Cases.Count(c => c.Passed).Should().Be(1);
    }

    [Fact]
    public async Task Runtime_exception_is_captured_as_case_error()
    {
        const string code = "public static bool IsBalanced(string s) => throw new System.InvalidOperationException(\"boom\");";

        var run = await Evaluator().RunAsync(code, Problem("balanced-brackets"), CancellationToken.None);

        run.Cases.Should().OnlyContain(c => !c.Passed && c.Error != null && c.Error.Contains("boom"));
    }

    [Fact]
    public async Task Missing_function_is_a_harness_failure()
    {
        const string code = "public static class Solution { public static int Add(int a, int b) => a + b; }";

        var run = await Evaluator().RunAsync(code, Problem("sum-two-numbers"), CancellationToken.None);

        run.Cases.Should().BeEmpty();
        run.Stderr.Should().Contain("'Sum'");
    }

    [Fact]
    public async Task Infinite_loop_times_out()
    {
        const string code = "public static int Sum(int a, int b) { while (true) { } }";

        var run = await Evaluator(runTimeoutSeconds: 1).RunAsync(code, Problem("sum-two-numbers"), CancellationToken.None);

        run.TimedOut.Should().BeTrue();
        run.Cases.Should().BeEmpty();
    }
}
