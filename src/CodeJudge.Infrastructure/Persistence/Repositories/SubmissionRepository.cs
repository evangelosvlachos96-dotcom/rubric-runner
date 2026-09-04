using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Common;
using CodeJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeJudge.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ISubmissionRepository"/>
public sealed class SubmissionRepository : ISubmissionRepository
{
    private readonly CodeJudgeDbContext _dbContext;

    public SubmissionRepository(CodeJudgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Submission submission, CancellationToken cancellationToken)
        => await _dbContext.Submissions.AddAsync(submission, cancellationToken);

    public Task<Submission?> GetByIdWithResultsAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.Submissions
            .AsNoTracking()
            .Include(s => s.Results)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<PagedResult<Submission>> GetPagedByUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Submissions
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Submission>(items, page, pageSize, totalCount);
    }
}
