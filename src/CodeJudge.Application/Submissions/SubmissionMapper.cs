using System.Text.Json;
using CodeJudge.Application.Problems;
using CodeJudge.Application.Submissions.Dtos;
using CodeJudge.Domain.Entities;

namespace CodeJudge.Application.Submissions;

/// <summary>Hand-written mapping between domain entities/catalog and the API DTOs (no AutoMapper, System Design §5.2).</summary>
public static class SubmissionMapper
{
    public static SubmissionDto ToDto(Submission submission) => new(
        submission.Id,
        submission.UserId,
        submission.ProblemId,
        submission.Language,
        submission.Status,
        submission.CreatedAt,
        submission.StartedAt,
        submission.CompletedAt,
        submission.ErrorMessage,
        submission.Results
            .OrderBy(r => r.RubricItem)
            .Select(ToDto)
            .ToList());

    public static SubmissionSummaryDto ToSummaryDto(Submission submission) => new(
        submission.Id,
        submission.ProblemId,
        submission.Language,
        submission.Status,
        submission.CreatedAt);

    public static ProblemDto ToDto(ProblemDefinition problem) => new(
        problem.Id,
        problem.Title,
        problem.Description,
        problem.Difficulty,
        problem.Signatures
            .Select(kvp => new ProblemSignatureDto(kvp.Key, kvp.Value.FunctionName, kvp.Value.SignatureText))
            .ToList(),
        problem.Cases
            .Where(c => c.IsSample)
            .Select(c => new ProblemSampleCaseDto(c.Id, c.Args, c.Expected))
            .ToList());

    private static EvaluationResultDto ToDto(EvaluationResult result) => new(
        result.RubricItem,
        result.Passed,
        result.Skipped,
        result.Message,
        ParseOutput(result.Output),
        result.TestsPassed,
        result.TestsTotal,
        result.DurationMs,
        result.EvaluatedAt);

    // The stored output is JSON text; re-parse it so it embeds as a real object in the response
    // rather than an escaped string. Detached via Clone so it survives the document's disposal.
    private static JsonElement? ParseOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
