namespace CodeJudge.Application.Abstractions;

/// <summary>The first restricted keyword found in submitted code, and the 1-based line it appears on.</summary>
public sealed record RestrictedKeywordMatch(string Keyword, int LineNumber);
