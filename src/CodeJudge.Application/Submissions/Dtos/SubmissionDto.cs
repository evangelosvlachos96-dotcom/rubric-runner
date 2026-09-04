using System.Text.Json;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Submissions.Dtos;

/// <summary>Full view of a submission and its rubric results (<c>GET /submissions/{id}</c>).</summary>
public sealed record SubmissionDto(
    Guid Id,
    string UserId,
    string ProblemId,
    Language Language,
    SubmissionStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    IReadOnlyList<EvaluationResultDto> Results);

/// <summary>One rubric result within a <see cref="SubmissionDto"/>.</summary>
public sealed record EvaluationResultDto(
    RubricItem RubricItem,
    bool Passed,
    bool Skipped,
    string? Message,
    JsonElement? Output,
    int? TestsPassed,
    int? TestsTotal,
    int? DurationMs,
    DateTime EvaluatedAt);

/// <summary>Compact view of a submission for the per-user history list (<c>GET /users/{id}/submissions</c>).</summary>
public sealed record SubmissionSummaryDto(
    Guid Id,
    string ProblemId,
    Language Language,
    SubmissionStatus Status,
    DateTime CreatedAt);
