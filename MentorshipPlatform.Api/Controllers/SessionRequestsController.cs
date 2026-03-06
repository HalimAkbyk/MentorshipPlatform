using MediatR;
using MentorshipPlatform.Application.SessionRequests.Commands.CreateSessionRequest;
using MentorshipPlatform.Application.SessionRequests.Commands.ApproveSessionRequest;
using MentorshipPlatform.Application.SessionRequests.Commands.RejectSessionRequest;
using MentorshipPlatform.Application.SessionRequests.Queries.GetMySessionRequests;
using MentorshipPlatform.Application.SessionRequests.Queries.GetMentorSessionRequests;
using MentorshipPlatform.Application.SessionRequests.Queries.GetAdminSessionRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/session-requests")]
public class SessionRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Ogrenci yeni seans talebi olusturur</summary>
    [Authorize(Roles = "Student")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequestCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { id = result.Data });
    }

    /// <summary>Ogrencinin kendi seans taleplerini listeler</summary>
    [Authorize(Roles = "Student")]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMySessionRequestsQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    /// <summary>Egitmenin kendisine gelen seans taleplerini listeler</summary>
    [Authorize(Roles = "Mentor")]
    [HttpGet("mentor")]
    public async Task<IActionResult> GetMentorRequests(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMentorSessionRequestsQuery(), ct);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    /// <summary>Admin tum seans taleplerini listeler</summary>
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminRequests([FromQuery] string? status, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAdminSessionRequestsQuery(status), ct);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    /// <summary>Seans talebini onayla (egitmen veya admin)</summary>
    [Authorize(Roles = "Mentor,Admin")]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ApproveSessionRequestCommand(id), ct);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { bookingId = result.Data });
    }

    /// <summary>Seans talebini reddet (egitmen veya admin)</summary>
    [Authorize(Roles = "Mentor,Admin")]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectSessionRequestBody? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new RejectSessionRequestCommand(id, body?.Reason), ct);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { success = true });
    }
}

public record RejectSessionRequestBody(string? Reason);
