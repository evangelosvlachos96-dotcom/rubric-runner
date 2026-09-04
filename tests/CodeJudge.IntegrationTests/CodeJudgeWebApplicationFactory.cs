using CodeJudge.Application.Abstractions;
using CodeJudge.Domain.Entities;
using CodeJudge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CodeJudge.IntegrationTests;

/// <summary>
/// Boots the API with the real pipeline but swaps PostgreSQL for EF InMemory and makes the worker
/// inert via a no-op claimer, so the HTTP contract can be tested without external dependencies
/// (System Design §12).
/// </summary>
public sealed class CodeJudgeWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "dev-api-key-change-me";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Drop the Npgsql context, its options, and its options-configuration (EF Core 10 registers
            // the provider via IDbContextOptionsConfiguration<T>, which accumulates otherwise).
            RemoveWhere(services, d =>
                d.ServiceType == typeof(CodeJudgeDbContext)
                || (d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) ?? false));

            services.AddDbContext<CodeJudgeDbContext>(options =>
                options.UseInMemoryDatabase("codejudge-integration-tests"));

            RemoveWhere(services, d => d.ServiceType == typeof(ISubmissionClaimer));
            services.AddScoped<ISubmissionClaimer, NoOpSubmissionClaimer>();
        });
    }

    private static void RemoveWhere(IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        foreach (var descriptor in services.Where(predicate).ToList())
        {
            services.Remove(descriptor);
        }
    }

    private sealed class NoOpSubmissionClaimer : ISubmissionClaimer
    {
        public Task<IReadOnlyList<Submission>> ClaimBatchAsync(
            int batchSize,
            string workerId,
            TimeSpan lockDuration,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Submission>>([]);
    }
}
