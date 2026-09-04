using CodeJudge.Application.Abstractions;
using CodeJudge.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace CodeJudge.Infrastructure.Security;

/// <summary>
/// Keyword deny-list security check (System Design §6.6). Per-language lists come from
/// <c>Security:RestrictedKeywords:{Language}</c>. Returns the first matched keyword (config order)
/// on the earliest line it appears.
/// </summary>
public sealed class RestrictedKeywordPolicy : IRestrictedKeywordPolicy
{
    private readonly IReadOnlyDictionary<Language, string[]> _keywords;

    public RestrictedKeywordPolicy(IConfiguration configuration)
    {
        _keywords = configuration
            .GetSection("Security:RestrictedKeywords")
            .Get<Dictionary<Language, string[]>>() ?? new Dictionary<Language, string[]>();
    }

    public RestrictedKeywordMatch? Check(Language language, string code)
    {
        if (string.IsNullOrEmpty(code) || !_keywords.TryGetValue(language, out var keywords) || keywords.Length == 0)
        {
            return null;
        }

        var lines = code.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var keyword in keywords)
            {
                if (lines[i].Contains(keyword, StringComparison.Ordinal))
                {
                    return new RestrictedKeywordMatch(keyword, i + 1);
                }
            }
        }

        return null;
    }
}
