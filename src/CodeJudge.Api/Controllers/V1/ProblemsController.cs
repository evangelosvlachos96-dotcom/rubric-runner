using Asp.Versioning;
using CodeJudge.Api.Models.Common;
using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Submissions;
using CodeJudge.Application.Submissions.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeJudge.Api.Controllers.V1;

/// <summary>The problem catalog a client can solve.</summary>
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class ProblemsController : ControllerBase
{
    private readonly IProblemCatalog _catalog;

    public ProblemsController(IProblemCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>List all problems with their per-language signatures and sample cases.</summary>
    /// <response code="200">The catalog.</response>
    /// <response code="401">Missing or invalid API key.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<IReadOnlyList<ProblemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult GetAll()
    {
        var problems = _catalog.GetAll().Select(SubmissionMapper.ToDto).ToList();
        return Ok(new ApiResult<IReadOnlyList<ProblemDto>>(problems));
    }
}
