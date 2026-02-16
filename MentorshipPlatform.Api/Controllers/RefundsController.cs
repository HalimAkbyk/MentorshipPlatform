using MediatR;
using MentorshipPlatform.Application.Refunds.Commands.InitiateRefund;
using MentorshipPlatform.Application.Refunds.Commands.ProcessRefund;
using MentorshipPlatform.Application.Refunds.Commands.RequestRefund;
using MentorshipPlatform.Application.Refunds.Queries.GetRefundRequests;
using MentorshipPlatform.Application.Refunds.Queries.GetStudentRefundRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/refunds")]
public class RefundsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RefundsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Student requests a refund for an order
    /// </summary>
    [HttpPost("request")]
    [Authorize]
    [ProducesResponseType(typeof(RefundRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestRefund([FromBody] RequestRefundCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Student views their refund request history
    /// </summary>
    [HttpGet("my-requests")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRefundRequests()
    {
        var result = await _mediator.Send(new GetStudentRefundRequestsQuery());
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Admin views all refund requests with optional status filter
    /// </summary>
    [HttpGet("admin/list")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefundRequests(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetRefundRequestsQuery(status, page, pageSize));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Admin approves or rejects a refund request
    /// </summary>
    [HttpPost("admin/{id}/process")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessRefund(
        Guid id,
        [FromBody] ProcessRefundBody body)
    {
        var command = new ProcessRefundCommand(id, body.IsApproved, body.OverrideAmount, body.AdminNotes);
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Admin directly initiates a refund (or goodwill credit)
    /// </summary>
    [HttpPost("admin/initiate")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InitiateRefund([FromBody] InitiateRefundCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { ok = true });
    }
}

public record ProcessRefundBody(
    bool IsApproved,
    decimal? OverrideAmount,
    string? AdminNotes);
