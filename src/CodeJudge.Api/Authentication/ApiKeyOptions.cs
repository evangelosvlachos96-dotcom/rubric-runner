using Microsoft.AspNetCore.Authentication;

namespace CodeJudge.Api.Authentication;

/// <summary>Options for the <c>ApiKey</c> authentication scheme.</summary>
public sealed class ApiKeyOptions : AuthenticationSchemeOptions
{
    /// <summary>The configured API key incoming requests are compared against.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
