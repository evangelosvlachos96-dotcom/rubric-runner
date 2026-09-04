using CodeJudge.Application.Abstractions;
using CodeJudge.Infrastructure.Evaluators;
using CodeJudge.Infrastructure.Persistence;
using CodeJudge.Infrastructure.Persistence.Repositories;
using CodeJudge.Infrastructure.Security;
using CodeJudge.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodeJudge.Infrastructure;

/// <summary>Composition root for the Infrastructure layer (System Design §5.3).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddEvaluation(services, configuration);
        return services;
    }

    private static void AddEvaluation(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EvaluationSettings>()
            .Bind(configuration.GetSection(EvaluationSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IRestrictedKeywordPolicy, RestrictedKeywordPolicy>();
        services.AddSingleton<ProcessRunner>();

        services.AddSingleton<ICodeEvaluator, CSharpEvaluator>();
        services.AddSingleton<ICodeEvaluator, PythonEvaluator>();
        services.AddSingleton<ICodeEvaluator, JavaScriptEvaluator>();

        services.AddHostedService<EvaluationWorker>();
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:DefaultConnection"];

        services.AddDbContext<CodeJudgeDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CodeJudgeDbContext>());
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ISubmissionClaimer, SubmissionClaimer>();
    }
}
