using CodeJudge.Application.Common;
using CodeJudge.Application.Submissions.Dtos;

namespace CodeJudge.Application.Submissions;

/// <summary>Application service for the submission use cases (System Design §6.1).</summary>
public interface ISubmissionService
{
    Task<SubmissionDto> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken);

    /// <summary>Loads a submission with its results. Throws <see cref="NotFoundException"/> if it does not exist.</summary>
    Task<SubmissionDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<SubmissionSummaryDto>> GetByUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
