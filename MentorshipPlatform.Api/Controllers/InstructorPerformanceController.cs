using MediatR;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetAccruals;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetInstructorPerformanceSummary;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetInstructorSessionLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/instructor/performance")]
[Authorize(Policy = "RequireMentorRole")]
public class InstructorPerformanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public InstructorPerformanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/instructor/performance/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? periodType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        // InstructorId = null => handler uses current user's ID; feature flag checked inside handler
        var result = await _mediator.Send(new GetInstructorPerformanceSummaryQuery(
            null, periodType, dateFrom, dateTo));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // GET /api/instructor/performance/accruals
    [HttpGet("accruals")]
    public async Task<IActionResult> GetAccruals(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // InstructorId = null => handler uses current user's ID; feature flag checked inside handler
        var result = await _mediator.Send(new GetAccrualsQuery(
            null, status, page, pageSize));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // GET /api/instructor/performance/session-logs
    [HttpGet("session-logs")]
    public async Task<IActionResult> GetSessionLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sessionType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        // InstructorId = null => handler uses current user's ID
        var result = await _mediator.Send(new GetInstructorSessionLogsQuery(
            null, page, pageSize, sessionType, dateFrom, dateTo));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }
}
