using System.Text.Json;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Submissions.Dtos;

/// <summary>Public view of a catalog problem (<c>GET /problems</c>): metadata, signatures and sample cases only.</summary>
public sealed record ProblemDto(
    string Id,
    string Title,
    string Description,
    string Difficulty,
    IReadOnlyList<ProblemSignatureDto> Signatures,
    IReadOnlyList<ProblemSampleCaseDto> SampleCases);

/// <summary>The function signature a solution must implement for one language.</summary>
public sealed record ProblemSignatureDto(Language Language, string FunctionName, string Signature);

/// <summary>A sample (publicly visible) test case for a problem.</summary>
public sealed record ProblemSampleCaseDto(int Id, JsonElement[] Args, JsonElement Expected);
