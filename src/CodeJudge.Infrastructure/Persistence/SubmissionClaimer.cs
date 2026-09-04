using CodeJudge.Application.Abstractions;
using CodeJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeJudge.Infrastructure.Persistence;

/// <summary>
/// Claims a batch of submissions atomically with the CTE + <c>UPDATE … RETURNING</c> query from
/// Database Design §8. <c>FOR UPDATE SKIP LOCKED</c> guarantees each row is claimed by exactly one
/// worker under concurrency. The returned entities are tracked, so the worker can mutate and save them.
/// </summary>
public sealed class SubmissionClaimer : ISubmissionClaimer
{
    private const string ClaimSql = """
        WITH candidates AS (
            SELECT id
            FROM   submissions
            WHERE  status = 'Pending'
               OR (status = 'Evaluating' AND locked_until < now())
            ORDER  BY created_at
            LIMIT  @n
            FOR UPDATE SKIP LOCKED
        )
        UPDATE submissions s
        SET    status        = 'Evaluating',
               locked_by     = @workerId,
               locked_until  = @lockUntil,
               attempt_count = s.attempt_count + 1,
               started_at    = COALESCE(s.started_at, now())
        FROM   candidates c
        WHERE  s.id = c.id
        RETURNING s.*;
        """;

    private readonly CodeJudgeDbContext _dbContext;

    public SubmissionClaimer(CodeJudgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Submission>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var lockUntil = DateTime.UtcNow.Add(lockDuration);

        var parameters = new[]
        {
            new NpgsqlParameter("n", batchSize),
            new NpgsqlParameter("workerId", workerId),
            new NpgsqlParameter("lockUntil", lockUntil),
        };

        return await _dbContext.Submissions
            .FromSqlRaw(ClaimSql, parameters)
            .ToListAsync(cancellationToken);
    }
}
