using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeJudge.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the model and generate migrations without
/// booting the API host. The connection string is only used to construct the model, not to connect.
/// </summary>
public sealed class CodeJudgeDbContextFactory : IDesignTimeDbContextFactory<CodeJudgeDbContext>
{
    public CodeJudgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CodeJudgeDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=codejudge;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CodeJudgeDbContext(options);
    }
}
