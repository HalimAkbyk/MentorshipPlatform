using MediatR;
using MentorshipPlatform.Application.Payouts.Commands.CreatePayoutRequest;
using MentorshipPlatform.Application.Payouts.Commands.ProcessPayoutRequest;
using MentorshipPlatform.Application.Payouts.Queries.GetAllPayoutRequests;
using MentorshipPlatform.Application.Payouts.Queries.GetMyPayoutRequests;
using MentorshipPlatform.Application.Payouts.Queries.GetPayoutSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
public class PayoutRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayoutRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Mentor Endpoints ───

    [HttpPost("api/payout-requests")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(typeof(PayoutRequestDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePayoutRequest([FromBody] CreatePayoutRequestBody body)
    {
        var result = await _mediator.Send(new CreatePayoutRequestCommand(body.Amount, body.Note));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpGet("api/payout-requests")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetMyPayoutRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMyPayoutRequestsQuery(page, pageSize));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpGet("api/payout-requests/settings")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetPayoutSettings()
    {
        var result = await _mediator.Send(new GetPayoutSettingsQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // ─── Admin Endpoints ───

    [HttpGet("api/admin/payout-requests")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> GetAllPayoutRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var result = await _mediator.Send(new GetAllPayoutRequestsQuery(page, pageSize, status, search));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpPut("api/admin/payout-requests/{id}/process")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> ProcessPayoutRequest(
        Guid id,
        [FromBody] ProcessPayoutRequestBody body)
    {
        var result = await _mediator.Send(new ProcessPayoutRequestCommand(id, body.Action, body.AdminNote));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { message = "İşlem başarılı" });
    }
}

public record CreatePayoutRequestBody(decimal Amount, string? Note = null);
public record ProcessPayoutRequestBody(string Action, string? AdminNote = null);
