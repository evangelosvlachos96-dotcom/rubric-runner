using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CodeJudge.Api.Models.Common;
using CodeJudge.Application.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CodeJudge.Api.Authentication;

/// <summary>
/// Authenticates requests by a static <c>X-Api-Key</c> header, compared in constant time
/// (System Design §6.8). Implemented as a proper <see cref="AuthenticationHandler{TOptions}"/> so
/// <c>[Authorize]</c> works unchanged and JWT Bearer is a drop-in replacement later.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!FixedTimeEquals(provided.ToString(), Options.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "api-client")],
            Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = SchemeName;
        Response.ContentType = "application/json";

        var result = new ApiResult<object>(CodeJudgeErrorCode.Unauthorized, "Missing or invalid API key.");
        await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result, ApiJson.Options));
    }

    private static bool FixedTimeEquals(string provided, string configured)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
