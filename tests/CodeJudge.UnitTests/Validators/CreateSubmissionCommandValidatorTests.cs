using CodeJudge.Application.Common;
using CodeJudge.Application.Problems;
using CodeJudge.Application.Submissions;
using CodeJudge.Application.Submissions.Validators;
using CodeJudge.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CodeJudge.UnitTests.Validators;

public sealed class CreateSubmissionCommandValidatorTests
{
    private readonly CreateSubmissionCommandValidator _validator = new(new ProblemCatalog());

    private static CreateSubmissionCommand Valid() =>
        new("user-1", "sum-two-numbers", Language.Python, "def sum_two(a, b): return a + b");

    [Fact]
    public void Valid_command_passes()
        => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Empty_user_id_fails()
        => _validator.Validate(Valid() with { UserId = "" }).IsValid.Should().BeFalse();

    [Fact]
    public void Invalid_user_id_characters_fail()
        => _validator.Validate(Valid() with { UserId = "bad id!" }).IsValid.Should().BeFalse();

    [Fact]
    public void Unknown_problem_fails_with_problem_not_found_code()
    {
        var result = _validator.Validate(Valid() with { ProblemId = "does-not-exist" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == nameof(CodeJudgeErrorCode.ProblemNotFound));
    }

    [Fact]
    public void Empty_code_fails()
        => _validator.Validate(Valid() with { Code = "" }).IsValid.Should().BeFalse();

    [Fact]
    public void Oversized_code_fails_with_code_too_large()
    {
        var result = _validator.Validate(Valid() with { Code = new string('x', 65537) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == nameof(CodeJudgeErrorCode.CodeTooLarge));
    }
}
