using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Submissions;

/// <summary>
/// Application-level input for creating a submission. The API request model maps onto this
/// record, which is what the validator (and service) operate on.
/// </summary>
public sealed record CreateSubmissionCommand(string UserId, string ProblemId, Language Language, string Code);
