using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Classes.Commands.CreateGroupClass;
using MentorshipPlatform.Application.Classes.Commands.EnrollInClass;
using MentorshipPlatform.Application.Classes.Commands.CancelGroupClass;
using MentorshipPlatform.Application.Classes.Commands.CompleteGroupClass;
using MentorshipPlatform.Application.Classes.Commands.CancelEnrollment;
using MentorshipPlatform.Application.Classes.Queries.GetGroupClasses;
using MentorshipPlatform.Application.Classes.Queries.GetGroupClassById;
using MentorshipPlatform.Application.Classes.Queries.GetMyGroupClasses;
using MentorshipPlatform.Application.Classes.Queries.GetMyEnrollments;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/classes")]
public class GroupClassesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagService _featureFlags;

    public GroupClassesController(IMediator mediator, IFeatureFlagService featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    /// <summary>
    /// Create a new group class (Mentor only)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateGroupClassCommand command)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlags.GroupClassesEnabled))
            return BadRequest(new { errors = new[] { "Grup dersleri gecici olarak devre disi birakilmistir." } });

        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { id = result.Data });
    }

    /// <summary>
    /// List published group classes (public)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<GroupClassListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetGroupClassesQuery(category, search, page, pageSize));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Get group class details
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GroupClassDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetGroupClassByIdQuery(id));
        if (!result.IsSuccess) return NotFound(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// List mentor's own group classes
    /// </summary>
    [HttpGet("my")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(typeof(List<MyGroupClassDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyClasses([FromQuery] string? status)
    {
        var result = await _mediator.Send(new GetMyGroupClassesQuery(status));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Enroll in a group class (Student only)
    /// </summary>
    [HttpPost("{id:guid}/enroll")]
    [Authorize(Policy = "RequireStudentRole")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enroll(Guid id)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlags.GroupClassesEnabled))
            return BadRequest(new { errors = new[] { "Grup dersleri gecici olarak devre disi birakilmistir." } });

        var result = await _mediator.Send(new EnrollInClassCommand(id));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { enrollmentId = result.Data });
    }

    /// <summary>
    /// Cancel a group class (Mentor only)
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelRequest? body)
    {
        var result = await _mediator.Send(new CancelGroupClassCommand(id, body?.Reason));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { success = true });
    }

    /// <summary>
    /// Complete a group class (Mentor only)
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(Guid id)
    {
        var result = await _mediator.Send(new CompleteGroupClassCommand(id));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { success = true });
    }

    /// <summary>
    /// List student's own enrollments
    /// </summary>
    [HttpGet("enrollments/my")]
    [Authorize(Policy = "RequireStudentRole")]
    [ProducesResponseType(typeof(List<MyEnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEnrollments()
    {
        var result = await _mediator.Send(new GetMyEnrollmentsQuery());
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Cancel an enrollment (Student only)
    /// </summary>
    [HttpPost("enrollments/{enrollmentId:guid}/cancel")]
    [Authorize(Policy = "RequireStudentRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelEnrollment(Guid enrollmentId, [FromBody] CancelRequest body)
    {
        var result = await _mediator.Send(new CancelEnrollmentCommand(enrollmentId, body.Reason));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { success = true });
    }
}

public record CancelRequest(string Reason);
