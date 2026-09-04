using CodeJudge.Domain.Entities;

namespace CodeJudge.Application.Abstractions;

/// <summary>
/// Atomically claims a batch of pending (or lock-expired) submissions for evaluation using
/// PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> (Database Design §8). Returns the claimed rows,
/// already transitioned to <c>Evaluating</c>.
/// </summary>
public interface ISubmissionClaimer
{
    Task<IReadOnlyList<Submission>> ClaimBatchAsync(
        int batchSize,
        string workerId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken);
}
