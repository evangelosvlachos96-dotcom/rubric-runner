using CodeJudge.Application.Common;
using CodeJudge.Domain.Entities;

namespace CodeJudge.Application.Abstractions;

/// <summary>
/// Persistence for the <see cref="Submission"/> aggregate. Only the queries the use cases
/// need — no generic repository (System Design §6.3).
/// </summary>
public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken);

    Task<Submission?> GetByIdWithResultsAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Submission>> GetPagedByUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
