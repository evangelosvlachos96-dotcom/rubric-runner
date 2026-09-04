using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Common;
using CodeJudge.Application.Submissions.Dtos;
using CodeJudge.Domain.Entities;

namespace CodeJudge.Application.Submissions;

/// <inheritdoc cref="ISubmissionService"/>
public sealed class SubmissionService : ISubmissionService
{
    private readonly ISubmissionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmissionService(ISubmissionRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SubmissionDto> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken)
    {
        var submission = Submission.Create(command.UserId, command.ProblemId, command.Language, command.Code);

        await _repository.AddAsync(submission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SubmissionMapper.ToDto(submission);
    }

    public async Task<SubmissionDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await _repository.GetByIdWithResultsAsync(id, cancellationToken)
            ?? throw new NotFoundException("Submission", id);

        return SubmissionMapper.ToDto(submission);
    }

    public async Task<PagedResult<SubmissionSummaryDto>> GetByUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var paged = await _repository.GetPagedByUserAsync(userId, page, pageSize, cancellationToken);

        return new PagedResult<SubmissionSummaryDto>(
            paged.Items.Select(SubmissionMapper.ToSummaryDto).ToList(),
            paged.Page,
            paged.PageSize,
            paged.TotalCount);
    }
}
