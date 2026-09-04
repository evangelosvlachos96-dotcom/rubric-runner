namespace CodeJudge.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.Submission"/> (System Design §7, Database Design §4).
/// <see cref="Completed"/> means the rubric ran (regardless of pass/fail);
/// <see cref="Error"/> means the system could not evaluate.
/// </summary>
public enum SubmissionStatus
{
    Pending,
    Evaluating,
    Completed,
    Error,
}
