using CodeJudge.Application.Abstractions;
using CodeJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeJudge.Infrastructure.Persistence;

/// <summary>
/// EF Core context for CodeJudge. Uses snake_case naming (Database Design §1) and doubles as the
/// application's <see cref="IUnitOfWork"/> — its <c>SaveChangesAsync</c> already satisfies the
/// interface (System Design §6.3).
/// </summary>
public sealed class CodeJudgeDbContext : DbContext, IUnitOfWork
{
    public CodeJudgeDbContext(DbContextOptions<CodeJudgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodeJudgeDbContext).Assembly);
    }
}
