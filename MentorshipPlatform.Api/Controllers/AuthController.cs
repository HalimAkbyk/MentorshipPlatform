using MediatR;
using MentorshipPlatform.Application.Auth.Commands.ExternalLogin;
using MentorshipPlatform.Application.Auth.Commands.Login;
using MentorshipPlatform.Application.Auth.Commands.RegisterUser;
using MentorshipPlatform.Application.Auth.Queries.GetMe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SignUp([FromBody] RegisterUserCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }
    
    [HttpPost("external-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ExternalLoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginCommand command)
    {
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var result = await _mediator.Send(new GetMeQuery());
        if (!result.IsSuccess) return Unauthorized(new { errors = result.Errors });
        return Ok(result.Data);
    }
}