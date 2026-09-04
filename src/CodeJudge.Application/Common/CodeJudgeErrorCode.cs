namespace CodeJudge.Application.Common;

/// <summary>
/// Stable, machine-readable error identifiers exposed in the <c>ApiResult.error.errorCode</c> field.
/// Gapped numbering per group: 1xxx generic, 2xxx submission, 3xxx problem/evaluation, 4xxx auth
/// (System Design §6.10).
/// </summary>
public enum CodeJudgeErrorCode
{
    Unknown = 0,
    ValidationFailed = 1000,
    NotFound = 1001,
    Conflict = 1002,
    SubmissionNotFound = 2001,
    CodeTooLarge = 2002,
    ProblemNotFound = 3001,
    UnsupportedLanguage = 3002,
    Unauthorized = 4001,
}
