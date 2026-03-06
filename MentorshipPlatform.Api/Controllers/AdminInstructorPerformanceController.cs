using MediatR;
using MentorshipPlatform.Application.InstructorPerformance.Commands.ApproveAccrual;
using MentorshipPlatform.Application.InstructorPerformance.Commands.CancelAccrual;
using MentorshipPlatform.Application.InstructorPerformance.Commands.ManageAccrualParameter;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetAccrualParameters;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetAccruals;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetInstructorPerformanceSummary;
using MentorshipPlatform.Application.InstructorPerformance.Queries.GetInstructorSessionLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/instructor-performance")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminInstructorPerformanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminInstructorPerformanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET /api/admin/instructor-performance/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid? instructorId = null,
        [FromQuery] string? periodType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var result = await _mediator.Send(new GetInstructorPerformanceSummaryQuery(
            instructorId, periodType, dateFrom, dateTo));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // GET /api/admin/instructor-performance/session-logs
    [HttpGet("session-logs")]
    public async Task<IActionResult> GetSessionLogs(
        [FromQuery] Guid? instructorId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sessionType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var result = await _mediator.Send(new GetInstructorSessionLogsQuery(
            instructorId, page, pageSize, sessionType, dateFrom, dateTo));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // GET /api/admin/instructor-performance/accruals
    [HttpGet("accruals")]
    public async Task<IActionResult> GetAccruals(
        [FromQuery] Guid? instructorId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetAccrualsQuery(
            instructorId, status, page, pageSize));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // POST /api/admin/instructor-performance/accruals/{id}/approve
    [HttpPost("accruals/{id:guid}/approve")]
    public async Task<IActionResult> ApproveAccrual(Guid id)
    {
        var result = await _mediator.Send(new ApproveAccrualCommand(id));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { message = "Hakediş onaylandı." });
    }

    // POST /api/admin/instructor-performance/accruals/{id}/cancel
    [HttpPost("accruals/{id:guid}/cancel")]
    public async Task<IActionResult> CancelAccrual(Guid id, [FromBody] CancelAccrualRequest? body = null)
    {
        var result = await _mediator.Send(new CancelAccrualCommand(id, body?.Notes));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { message = "Hakediş iptal edildi." });
    }

    // GET /api/admin/instructor-performance/accrual-parameters
    [HttpGet("accrual-parameters")]
    public async Task<IActionResult> GetAccrualParameters()
    {
        var result = await _mediator.Send(new GetAccrualParametersQuery());
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // POST /api/admin/instructor-performance/accrual-parameters
    [HttpPost("accrual-parameters")]
    public async Task<IActionResult> ManageAccrualParameter([FromBody] ManageAccrualParameterRequest body)
    {
        var result = await _mediator.Send(new ManageAccrualParameterCommand(
            body.InstructorId,
            body.PrivateLessonRate,
            body.GroupLessonRate,
            body.VideoContentRate,
            body.BonusThresholdLessons,
            body.BonusPercentage));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { id = result.Data, message = "Hakediş parametresi kaydedildi." });
    }
}

// Request DTOs
public class CancelAccrualRequest
{
    public string? Notes { get; set; }
}

public class ManageAccrualParameterRequest
{
    public Guid? InstructorId { get; set; }
    public decimal PrivateLessonRate { get; set; }
    public decimal GroupLessonRate { get; set; }
    public decimal VideoContentRate { get; set; }
    public int? BonusThresholdLessons { get; set; }
    public decimal? BonusPercentage { get; set; }
}
