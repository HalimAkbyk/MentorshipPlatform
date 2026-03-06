using MediatR;
using MentorshipPlatform.Application.FreeSessions.Commands.CreateFreeSession;
using MentorshipPlatform.Application.FreeSessions.Commands.EndFreeSession;
using MentorshipPlatform.Application.FreeSessions.Queries.GetEligibleStudents;
using MentorshipPlatform.Application.FreeSessions.Queries.GetMyFreeSessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FreeSessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FreeSessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a free session (mentor only). Deducts 1 PrivateLesson credit from the student.</summary>
    [HttpPost]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> Create([FromBody] CreateFreeSessionCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Get students eligible for a free session (have PrivateLesson credits).</summary>
    [HttpGet("eligible-students")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GetEligibleStudents()
    {
        var result = await _mediator.Send(new GetEligibleStudentsQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Get my free sessions (as mentor or student).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyFreeSessions()
    {
        var result = await _mediator.Send(new GetMyFreeSessionsQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>End a free session (mentor only).</summary>
    [HttpPost("{id}/end")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> End(Guid id)
    {
        var result = await _mediator.Send(new EndFreeSessionCommand(id));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok();
    }
}
