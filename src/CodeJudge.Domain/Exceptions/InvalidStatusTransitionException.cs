using CodeJudge.Domain.Enums;

namespace CodeJudge.Domain.Exceptions;

/// <summary>
/// Thrown when a <see cref="Entities.Submission"/> is asked to move between
/// two states that the lifecycle state machine does not allow (Database Design §4).
/// Mapped by the API to <c>409 / Conflict</c>.
/// </summary>
public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(SubmissionStatus from, string attemptedTransition)
        : base($"Cannot {attemptedTransition} a submission in status '{from}'.")
    {
        From = from;
        AttemptedTransition = attemptedTransition;
    }

    public SubmissionStatus From { get; }

    public string AttemptedTransition { get; }
}
