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

    [Theory]
    [InlineData("two-sum", true)]
    [InlineData("nope", false)]
    public void Exists_reflects_membership(string id, bool expected)
        => _catalog.Exists(id).Should().Be(expected);
}
