using Asp.Versioning;
using CodeJudge.Api.Models.Common;
using CodeJudge.Api.Models.Requests;
using CodeJudge.Application.Submissions;
using CodeJudge.Application.Submissions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeJudge.Api.Controllers.V1;

/// <summary>Create submissions and retrieve their status and rubric results.</summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    /// <summary>Submit a code solution for asynchronous evaluation.</summary>
    /// <param name="request">The submission to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">The submission was accepted and queued; status is <c>pending</c>.</response>
    /// <response code="400">Validation failed (unknown problem, unsupported language, empty/oversized code).</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<SubmissionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await _submissionService.CreateAsync(request.ToCommand(), cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = dto.Id, version = "1.0" },
            new ApiResult<SubmissionDto>(dto, "Submission accepted and queued for evaluation."));
    }

    /// <summary>Get a submission and its rubric results by id.</summary>
    /// <param name="id">The submission id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The submission. <c>results</c> is empty while pending/evaluating.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="404">No submission exists with that id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResult<SubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _submissionService.GetAsync(id, cancellationToken);
        return Ok(new ApiResult<SubmissionDto>(dto));
    }
}
