using CodeJudge.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodeJudge.Api.Swagger;

/// <summary>
/// Adds the <c>ApiKey</c> security requirement to every operation that is not anonymous, so the
/// Swagger UI padlock sends the <c>X-Api-Key</c> header (System Design §10).
/// </summary>
public sealed class ApiKeySecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var allowsAnonymous = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()
            || (context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() ?? false);

        if (allowsAnonymous)
        {
            return;
        }

        var scheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = ApiKeyAuthenticationHandler.SchemeName,
            },
        };

        operation.Security.Add(new OpenApiSecurityRequirement { [scheme] = [] });
    }
}
