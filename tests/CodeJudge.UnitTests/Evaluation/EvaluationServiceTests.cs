using System.Text.Json;
using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Common;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Entities;
using CodeJudge.Domain.Enums;
using CodeJudge.Domain.Exceptions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CodeJudge.UnitTests.Evaluation;

/// <summary>
/// Rubric semantics of <see cref="EvaluationService"/> using a fake <see cref="ICodeEvaluator"/>,
/// so no language runtime is needed (System Design §12).
/// </summary>
public sealed class EvaluationServiceTests
{
    private const string ProblemId = "sum-two-numbers";

    private readonly ProblemCatalog _catalog = new();
    private readonly IRestrictedKeywordPolicy _keywordPolicy = Substitute.For<IRestrictedKeywordPolicy>();
    private readonly ICodeEvaluator _evaluator = Substitute.For<ICodeEvaluator>();

    public EvaluationServiceTests()
    {
        _evaluator.Language.Returns(Language.Python);
        _keywordPolicy.Check(Arg.Any<Language>(), Arg.Any<string>()).Returns((RestrictedKeywordMatch?)null);
    }

    private EvaluationService Service() => new(_catalog, _keywordPolicy, [_evaluator]);

    private static Submission NewSubmission(string problemId = ProblemId, Language language = Language.Python) =>
        Submission.Create("user-1", problemId, language, "def sum_two(a, b): return a + b");

    private ProblemDefinition Problem => _catalog.Find(ProblemId)!;

    private void CompileSucceeds() =>
        _evaluator.CompileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CompileResult.Ok(durationMs: 12));

    private void HarnessReturns(params bool[] passedPerCase)
    {
        var cases = Problem.Cases
            .Select((c, i) => new TestCaseResult(
                c.Id,
                c.Expected,
                passedPerCase[i] ? c.Expected : JsonSerializer.SerializeToElement(-1),
                passedPerCase[i],
                Error: null,
                DurationMs: 0.1))
            .ToList();

        _evaluator.RunAsync(Arg.Any<string>(), Arg.Any<ProblemDefinition>(), Arg.Any<CancellationToken>())
            .Returns(new HarnessRunResult(cases, TimedOut: false, Stderr: null, DurationMs: 40));
    }

    private static EvaluationResult Item(IReadOnlyList<EvaluationResult> results, RubricItem item)
        => results.Single(r => r.RubricItem == item);

    // ---- The two scenarios required by the brief -------------------------------------------------

    [Fact]
    public async Task Compile_failure_fails_Compiles_and_skips_Test()
    {
        _evaluator.CompileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CompileResult.Failure(
                [new CompileDiagnostic(1, 5, "CS1002", "; expected")],
                durationMs: 30));

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        results.Should().HaveCount(3);
        Item(results, RubricItem.Security).Passed.Should().BeTrue();

        var compiles = Item(results, RubricItem.Compiles);
        compiles.Passed.Should().BeFalse();
        compiles.Skipped.Should().BeFalse();
        compiles.Message.Should().Be("; expected");
        compiles.Output.Should().Contain("CS1002");

        var test = Item(results, RubricItem.Test);
        test.Passed.Should().BeFalse();
        test.Skipped.Should().BeTrue();

        await _evaluator.DidNotReceive()
            .RunAsync(Arg.Any<string>(), Arg.Any<ProblemDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Correct_solution_passes_all_three_rubric_items()
    {
        CompileSucceeds();
        HarnessReturns(true, true, true, true);

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Passed && !r.Skipped);

        var test = Item(results, RubricItem.Test);
        test.TestsPassed.Should().Be(4);
        test.TestsTotal.Should().Be(4);
        test.Message.Should().Be("4/4 test cases passed");
        test.Output.Should().Contain("\"cases\"");
    }

    // ---- Remaining rubric paths ----------------------------------------------------------------

    [Fact]
    public async Task Restricted_keyword_fails_Security_and_skips_the_other_two()
    {
        _keywordPolicy.Check(Language.Python, Arg.Any<string>())
            .Returns(new RestrictedKeywordMatch("os.system", 2));

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        results.Should().HaveCount(3);

        var security = Item(results, RubricItem.Security);
        security.Passed.Should().BeFalse();
        security.Skipped.Should().BeFalse();
        security.Message.Should().Be("Restricted keyword 'os.system' found on line 2.");

        Item(results, RubricItem.Compiles).Skipped.Should().BeTrue();
        Item(results, RubricItem.Test).Skipped.Should().BeTrue();

        await _evaluator.DidNotReceive().CompileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _evaluator.DidNotReceive()
            .RunAsync(Arg.Any<string>(), Arg.Any<ProblemDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Partial_pass_reports_k_of_N_and_fails_Test()
    {
        CompileSucceeds();
        HarnessReturns(true, true, false, true);

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        var test = Item(results, RubricItem.Test);
        test.Passed.Should().BeFalse();
        test.Skipped.Should().BeFalse();
        test.Message.Should().Be("3/4 test cases passed");
        test.TestsPassed.Should().Be(3);
        test.TestsTotal.Should().Be(4);
    }

    [Fact]
    public async Task Timed_out_run_fails_Test_with_timeout_message()
    {
        CompileSucceeds();
        _evaluator.RunAsync(Arg.Any<string>(), Arg.Any<ProblemDefinition>(), Arg.Any<CancellationToken>())
            .Returns(new HarnessRunResult([], TimedOut: true, Stderr: null, DurationMs: 5000));

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        var test = Item(results, RubricItem.Test);
        test.Passed.Should().BeFalse();
        test.Message.Should().Be("Timed out after 5000 ms");
        test.TestsPassed.Should().Be(0);
        test.TestsTotal.Should().Be(4);
    }

    [Fact]
    public async Task Harness_crash_with_no_cases_fails_Test_with_stderr()
    {
        CompileSucceeds();
        _evaluator.RunAsync(Arg.Any<string>(), Arg.Any<ProblemDefinition>(), Arg.Any<CancellationToken>())
            .Returns(new HarnessRunResult([], TimedOut: false, Stderr: "NameError: name 'sum_two' is not defined\n", DurationMs: 20));

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        var test = Item(results, RubricItem.Test);
        test.Passed.Should().BeFalse();
        test.Message.Should().Be("NameError: name 'sum_two' is not defined");
        test.TestsTotal.Should().Be(4);
    }

    [Fact]
    public async Task Results_always_cover_each_rubric_item_exactly_once()
    {
        CompileSucceeds();
        HarnessReturns(false, false, false, false);

        var results = await Service().EvaluateAsync(NewSubmission(), CancellationToken.None);

        results.Select(r => r.RubricItem).Should().BeEquivalentTo(Enum.GetValues<RubricItem>());
    }

    // ---- System errors surface as exceptions, not rubric results -------------------------------

    [Fact]
    public async Task Unknown_problem_throws_NotFound()
    {
        var act = () => Service().EvaluateAsync(NewSubmission(problemId: "nope"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Missing_evaluator_for_language_throws_UnsupportedLanguage()
    {
        var act = () => Service().EvaluateAsync(NewSubmission(language: Language.JavaScript), CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedLanguageException>();
    }
}
