using System.Reflection;
using Asp.Versioning.ApiExplorer;
using CodeJudge.Api.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodeJudge.Api.Swagger;

/// <summary>
/// Configures Swashbuckle: one Swagger document per API version, XML comments, the <c>ApiKey</c>
/// security definition and annotations support (System Design §10).
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "CodeJudge API",
                Version = description.ApiVersion.ToString(),
                Description = "Submit code solutions and retrieve asynchronous rubric evaluation results."
                    + (description.IsDeprecated ? " This API version is deprecated." : string.Empty),
            });
        }

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }

        options.EnableAnnotations();

        // OpenAPI 3.0 ignores siblings of $ref, which drops the <example> on enum-typed properties
        // (e.g. CreateSubmissionRequest.Language); allOf-wrapping keeps the examples in the request body.
        options.UseAllOfToExtendReferenceSchemas();

        options.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
        {
            Name = ApiKeyAuthenticationHandler.HeaderName,
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Description = "API key supplied in the X-Api-Key header.",
        });

        options.OperationFilter<ApiKeySecurityOperationFilter>();
    }
}
