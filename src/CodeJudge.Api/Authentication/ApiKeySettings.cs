namespace CodeJudge.Api.Authentication;

/// <summary>Bound from the <c>ApiKeySettings</c> configuration section.</summary>
public sealed class ApiKeySettings
{
    public const string SectionName = "ApiKeySettings";

    public string Key { get; set; } = string.Empty;
}
