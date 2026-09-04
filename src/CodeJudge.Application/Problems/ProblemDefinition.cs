using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Problems;

/// <summary>
/// A coding problem: its metadata, the per-language function signature a solution must
/// implement, and the test cases the harness runs. Problems are data, not code paths —
/// adding one is a single catalog entry (System Design §6.5).
/// </summary>
public sealed record ProblemDefinition(
    string Id,
    string Title,
    string Description,
    string Difficulty,
    IReadOnlyDictionary<Language, LanguageSignature> Signatures,
    IReadOnlyList<TestCase> Cases);
