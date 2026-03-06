using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MentorshipPlatform.Application.Packages.Commands.CreatePackage;
using MentorshipPlatform.Application.Packages.Commands.UpdatePackage;
using MentorshipPlatform.Application.Packages.Commands.TogglePackage;
using MentorshipPlatform.Application.Packages.Queries.GetPackages;
using MentorshipPlatform.Application.Packages.Queries.GetPackageById;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/packages")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminPackageController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminPackageController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List all packages (including inactive)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetPackagesQuery(IncludeInactive: true));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Get package by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetPackageByIdQuery(id));
        if (!result.IsSuccess) return NotFound(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Create a new package
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePackageCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { id = result.Data });
    }

    /// <summary>
    /// Update an existing package
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePackageCommand command)
    {
        if (id != command.PackageId)
            return BadRequest(new { errors = new[] { "Route ID ile body ID uyuşmuyor." } });

        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { message = "Paket güncellendi." });
    }

    /// <summary>
    /// Activate or deactivate a package
    /// </summary>
    [HttpPost("{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Toggle(Guid id, [FromBody] TogglePackageRequest request)
    {
        var result = await _mediator.Send(new TogglePackageCommand(id, request.IsActive));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { message = request.IsActive ? "Paket aktifleştirildi." : "Paket devre dışı bırakıldı." });
    }
}

public class TogglePackageRequest
{
    public bool IsActive { get; set; }
}
