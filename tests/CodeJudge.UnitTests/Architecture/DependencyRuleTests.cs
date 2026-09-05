using CodeJudge.Application.Evaluation;
using CodeJudge.Domain.Entities;
using CodeJudge.Infrastructure.Evaluators;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace CodeJudge.UnitTests.Architecture;

/// <summary>
/// Compiler-enforced layering, checked once more at the type level so a stray <c>using</c> cannot
/// sneak an inward dependency past a project reference (System Design §5.3, §12).
/// </summary>
public sealed class DependencyRuleTests
{
    private const string Domain = "CodeJudge.Domain";
    private const string Application = "CodeJudge.Application";
    private const string Infrastructure = "CodeJudge.Infrastructure";
    private const string Api = "CodeJudge.Api";

    [Fact]
    public void Domain_depends_on_nothing_in_the_solution()
    {
        var result = Types.InAssembly(typeof(Submission).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Domain_uses_no_third_party_packages()
    {
        var result = Types.InAssembly(typeof(Submission).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "FluentValidation", "Npgsql", "Microsoft.Extensions")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(typeof(EvaluationService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Application_does_not_depend_on_EF_Core()
    {
        var result = Types.InAssembly(typeof(EvaluationService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(typeof(ProcessRunner).Assembly)
            .ShouldNot()
            .HaveDependencyOn(Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    private static string Because(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
