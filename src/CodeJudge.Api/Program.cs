using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using CodeJudge.Api.Authentication;
using CodeJudge.Api.Filters;
using CodeJudge.Api.Middleware;
using CodeJudge.Api.Swagger;
using CodeJudge.Application;
using CodeJudge.Infrastructure;
using CodeJudge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Sinks and levels come from the Serilog configuration section (appsettings.json).
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationActionFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

// All 400s flow through ExceptionMiddleware as ApiResult, not the built-in model-state response.
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

// Generated links (e.g. the 201 Location header) use the documented lowercase form /api/v1/submissions/{id}.
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        options => options.ApiKey = builder.Configuration[$"{ApiKeySettings.SectionName}:Key"] ?? string.Empty);

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration["ConnectionStrings:DefaultConnection"] ?? string.Empty);

var app = builder.Build();

await ApplyMigrationsAsync(app);

app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint(
            $"/swagger/{description.GroupName}/swagger.json",
            description.GroupName.ToUpperInvariant());
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
return;

// Applies pending migrations on startup in Development, guarded by config and skipped for
// non-relational providers (e.g. the EF InMemory provider used by integration tests).
static async Task ApplyMigrationsAsync(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CodeJudgeDbContext>();

    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
}

/// <summary>Exposed so the integration test's <c>WebApplicationFactory</c> can reference the entry point.</summary>
public partial class Program;
