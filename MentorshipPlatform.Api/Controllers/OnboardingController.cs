using MediatR;
using MentorshipPlatform.Application.Onboarding.Commands.SaveStudentOnboarding;
using MentorshipPlatform.Application.Onboarding.Commands.SaveMentorOnboarding;
using MentorshipPlatform.Application.Onboarding.Queries.GetStudentOnboarding;
using MentorshipPlatform.Application.Onboarding.Queries.GetMentorOnboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;

    public OnboardingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Student Onboarding ───

    [HttpGet("student")]
    public async Task<IActionResult> GetStudentOnboarding()
    {
        var result = await _mediator.Send(new GetStudentOnboardingQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpPut("student")]
    public async Task<IActionResult> SaveStudentOnboarding([FromBody] SaveStudentOnboardingCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // ─── Mentor Onboarding ───

    [HttpGet("mentor")]
    public async Task<IActionResult> GetMentorOnboarding()
    {
        var result = await _mediator.Send(new GetMentorOnboardingQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpPut("mentor")]
    public async Task<IActionResult> SaveMentorOnboarding([FromBody] SaveMentorOnboardingCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }
}
