using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Problems;

/// <summary>
/// The function a solution must implement for a given <see cref="Language"/>.
/// <see cref="FunctionName"/> is the name the harness invokes; <see cref="SignatureText"/>
/// is the human-readable signature shown to the client.
/// </summary>
public sealed record LanguageSignature(string FunctionName, string SignatureText);
