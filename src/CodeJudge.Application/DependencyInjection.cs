using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Application.Submissions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CodeJudge.Application;

/// <summary>Composition root for the Application layer (System Design §5.3).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<EvaluationService>();

        services.AddSingleton<IProblemCatalog, ProblemCatalog>();

        services.AddValidatorsFromAssemblyContaining<CreateSubmissionCommand>();

        return services;
    }
}
