using CodeJudge.Domain.Entities;
using CodeJudge.Domain.Enums;
using CodeJudge.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CodeJudge.UnitTests.Domain;

public sealed class SubmissionStateMachineTests
{
    private static Submission NewSubmission() =>
        Submission.Create("user-1", "sum-two-numbers", Language.Python, "def sum_two(a, b): return a + b");

    private static IEnumerable<EvaluationResult> ThreeResults() =>
    [
        EvaluationResult.Pass(RubricItem.Security),
        EvaluationResult.Pass(RubricItem.Compiles),
        EvaluationResult.Pass(RubricItem.Test),
    ];

    [Fact]
    public void Create_starts_pending()
    {
        var submission = NewSubmission();

        submission.Status.Should().Be(SubmissionStatus.Pending);
        submission.AttemptCount.Should().Be(0);
        submission.StartedAt.Should().BeNull();
    }

    [Fact]
    public void Claim_from_pending_moves_to_evaluating()
    {
        var submission = NewSubmission();

        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));

        submission.Status.Should().Be(SubmissionStatus.Evaluating);
        submission.AttemptCount.Should().Be(1);
        submission.StartedAt.Should().NotBeNull();
        submission.LockedBy.Should().Be("worker-1");
    }

    [Fact]
    public void Claim_while_locked_and_not_expired_throws()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));

        var act = () => submission.Claim("worker-2", DateTime.UtcNow.AddSeconds(90));

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Claim_after_lock_expiry_is_allowed_and_increments_attempts()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(-1));

        submission.Claim("worker-2", DateTime.UtcNow.AddSeconds(90));

        submission.Status.Should().Be(SubmissionStatus.Evaluating);
        submission.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void Complete_from_evaluating_with_three_results_moves_to_completed()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));

        submission.Complete(ThreeResults());

        submission.Status.Should().Be(SubmissionStatus.Completed);
        submission.Results.Should().HaveCount(3);
        submission.CompletedAt.Should().NotBeNull();
        submission.LockedBy.Should().BeNull();
    }

    [Fact]
    public void Complete_requires_all_three_rubric_items()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));

        var act = () => submission.Complete([EvaluationResult.Pass(RubricItem.Security)]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_from_pending_throws()
    {
        var submission = NewSubmission();

        var act = () => submission.Complete(ThreeResults());

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Fail_from_evaluating_moves_to_error()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));

        submission.Fail("runtime not available");

        submission.Status.Should().Be(SubmissionStatus.Error);
        submission.ErrorMessage.Should().Be("runtime not available");
    }

    [Fact]
    public void Fail_from_pending_throws()
    {
        var submission = NewSubmission();

        var act = () => submission.Fail("boom");

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Claim_after_completed_throws()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));
        submission.Complete(ThreeResults());

        var act = () => submission.Claim("worker-2", DateTime.UtcNow.AddSeconds(90));

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Claim_after_error_throws()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));
        submission.Fail("boom");

        var act = () => submission.Claim("worker-2", DateTime.UtcNow.AddSeconds(90));

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Complete_twice_throws()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));
        submission.Complete(ThreeResults());

        var act = () => submission.Complete(ThreeResults());

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Fail_after_completed_throws()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));
        submission.Complete(ThreeResults());

        var act = () => submission.Fail("too late");

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Complete_attaches_results_to_the_submission()
    {
        var submission = NewSubmission();
        submission.Claim("worker-1", DateTime.UtcNow.AddSeconds(90));

        submission.Complete(ThreeResults());

        submission.Results.Should().OnlyContain(r => r.SubmissionId == submission.Id);
    }

    [Theory]
    [InlineData("", "sum-two-numbers", "code")]
    [InlineData("user-1", "", "code")]
    [InlineData("user-1", "sum-two-numbers", "")]
    public void Create_rejects_blank_required_fields(string userId, string problemId, string code)
    {
        var act = () => Submission.Create(userId, problemId, Language.Python, code);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_rejects_undefined_language()
    {
        var act = () => Submission.Create("user-1", "sum-two-numbers", (Language)42, "code");

        act.Should().Throw<UnsupportedLanguageException>();
    }
}
