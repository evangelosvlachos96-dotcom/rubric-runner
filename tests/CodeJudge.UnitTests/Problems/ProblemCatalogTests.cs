using System.Text.Json;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CodeJudge.UnitTests.Problems;

public sealed class ProblemCatalogTests
{
    private readonly ProblemCatalog _catalog = new();

    [Fact]
    public void Catalog_contains_the_four_v1_problems()
    {
        _catalog.GetAll().Select(p => p.Id)
            .Should().BeEquivalentTo("sum-two-numbers", "reverse-words", "balanced-brackets", "two-sum");
    }

    [Fact]
    public void Every_problem_has_a_signature_for_every_language()
    {
        foreach (var problem in _catalog.GetAll())
        {
            foreach (var language in Enum.GetValues<Language>())
            {
                problem.Signatures.Should().ContainKey(language,
                    "problem '{0}' must define a signature for {1}", problem.Id, language);
            }
        }
    }

    [Fact]
    public void Every_problem_has_at_least_one_sample_case()
    {
        foreach (var problem in _catalog.GetAll())
        {
            problem.Cases.Should().Contain(c => c.IsSample, "problem '{0}' should expose a sample", problem.Id);
        }
    }

    [Fact]
    public void Every_expected_value_and_argument_is_valid_self_contained_JSON()
    {
        foreach (var problem in _catalog.GetAll())
        {
            problem.Cases.Should().NotBeEmpty("problem '{0}' needs test cases", problem.Id);

            foreach (var testCase in problem.Cases)
            {
                testCase.Expected.ValueKind.Should().NotBe(JsonValueKind.Undefined,
                    "case {0} of '{1}' must have an expected value", testCase.Id, problem.Id);

                var act = () => JsonDocument.Parse(testCase.Expected.GetRawText());
                act.Should().NotThrow("expected value of case {0} in '{1}' must be valid JSON", testCase.Id, problem.Id);

                foreach (var arg in testCase.Args)
                {
                    arg.ValueKind.Should().NotBe(JsonValueKind.Undefined);
                }
            }
        }
    }

    [Fact]
    public void Case_ids_are_unique_within_a_problem()
    {
        foreach (var problem in _catalog.GetAll())
        {
            problem.Cases.Select(c => c.Id).Should().OnlyHaveUniqueItems("problem '{0}'", problem.Id);
        }
    }

    [Fact]
    public void Find_is_case_insensitive_and_null_safe()
    {
        _catalog.Find("TWO-SUM").Should().NotBeNull();
        _catalog.Find("nope").Should().BeNull();
    }

    [Theory]
    [InlineData("two-sum", true)]
    [InlineData("nope", false)]
    public void Exists_reflects_membership(string id, bool expected)
        => _catalog.Exists(id).Should().Be(expected);
}
