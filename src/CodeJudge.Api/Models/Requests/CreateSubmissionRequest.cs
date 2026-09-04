using CodeJudge.Application.Submissions;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Api.Models.Requests;

/// <summary>Request body for <c>POST /api/v1/submissions</c>.</summary>
public sealed class CreateSubmissionRequest
{
    /// <summary>Client-asserted identifier of the submitting user.</summary>
    /// <example>user-123</example>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Catalog problem id the code solves (see <c>GET /api/v1/problems</c>).</summary>
    /// <example>sum-two-numbers</example>
    public string ProblemId { get; init; } = string.Empty;

    /// <summary>Language the code is written in.</summary>
    /// <example>Python</example>
    public Language Language { get; init; }

    /// <summary>The source code implementing the problem's function signature.</summary>
    /// <example>def sum_two(a, b):\n    return a + b</example>
    public string Code { get; init; } = string.Empty;

    /// <summary>Maps this API request to the application-level command the validator and service use.</summary>
    public CreateSubmissionCommand ToCommand() => new(UserId, ProblemId, Language, Code);
}
