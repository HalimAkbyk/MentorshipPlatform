using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MentorshipPlatform.Application.Packages.Queries.GetPackages;
using MentorshipPlatform.Application.Packages.Queries.GetPackageById;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/packages")]
public class PackagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PackagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List active packages (public, no auth required)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<PackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePackages()
    {
        var result = await _mediator.Send(new GetPackagesQuery(IncludeInactive: false));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Get package by ID (public)
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetPackageByIdQuery(id));
        if (!result.IsSuccess) return NotFound(new { errors = result.Errors });
        return Ok(result.Data);
    }
}
