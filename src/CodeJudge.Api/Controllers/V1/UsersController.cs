using Asp.Versioning;
using CodeJudge.Api.Models.Common;
using CodeJudge.Application.Common;
using CodeJudge.Application.Submissions;
using CodeJudge.Application.Submissions.Dtos;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeJudge.Api.Controllers.V1;

/// <summary>Per-user submission history.</summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private const int MaxPageSize = 100;

    private readonly ISubmissionService _submissionService;

    public UsersController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    /// <summary>List a user's submissions, most recent first, paginated.</summary>
    /// <param name="userId">The user id whose submissions to list.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Page size (1–100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">A page of submission summaries.</response>
    /// <response code="400">Invalid paging parameters.</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpGet("{userId}/submissions")]
    [ProducesResponseType(typeof(ApiResult<PagedResult<SubmissionSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSubmissions(
        string userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ValidationException([new ValidationFailure(nameof(page), "Page must be 1 or greater.")]);
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(pageSize), $"Page size must be between 1 and {MaxPageSize}.")]);
        }

        var result = await _submissionService.GetByUserAsync(userId, page, pageSize, cancellationToken);
        return Ok(new ApiResult<PagedResult<SubmissionSummaryDto>>(result));
    }
}
