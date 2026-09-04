using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Abstractions;

/// <summary>
/// The security rubric check: scans code for per-language restricted keywords
/// (System Design §6.6). Returns the first match, or <c>null</c> if the code is clean.
/// </summary>
public interface IRestrictedKeywordPolicy
{
    RestrictedKeywordMatch? Check(Language language, string code);
}
