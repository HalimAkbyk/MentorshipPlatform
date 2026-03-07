using MediatR;
using MentorshipPlatform.Application.SessionPlans.Commands.AddSessionPlanMaterial;
using MentorshipPlatform.Application.SessionPlans.Commands.CompleteSessionPlan;
using MentorshipPlatform.Application.SessionPlans.Commands.CreateSessionPlan;
using MentorshipPlatform.Application.SessionPlans.Commands.DeleteSessionPlan;
using MentorshipPlatform.Application.SessionPlans.Commands.RemoveSessionPlanMaterial;
using MentorshipPlatform.Application.SessionPlans.Commands.ShareSessionPlan;
using MentorshipPlatform.Application.SessionPlans.Commands.UpdateSessionPlan;
using MentorshipPlatform.Application.SessionPlans.Queries.GetMySessionPlans;
using MentorshipPlatform.Application.SessionPlans.Commands.CreateFromTemplate;
using MentorshipPlatform.Application.SessionPlans.Commands.SaveAsTemplate;
using MentorshipPlatform.Application.SessionPlans.Queries.GetMyTemplates;
using MentorshipPlatform.Application.SessionPlans.Queries.GetSessionPlanByBooking;
using MentorshipPlatform.Application.SessionPlans.Queries.GetSessionPlanByGroupClass;
using MentorshipPlatform.Application.SessionPlans.Queries.GetSessionPlanById;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/session-plans")]
public class SessionPlansController : ControllerBase
{
    private readonly IMediator _mediator;

    public SessionPlansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Yeni oturum plani olustur</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSessionPlanCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Oturum planlarimi listele</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPlans(
        [FromQuery] SessionPlanStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMySessionPlansQuery(status, search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Oturum plani detayi</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionPlanByIdQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Oturum plani guncelle</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionPlanRequest body, CancellationToken ct)
    {
        var command = new UpdateSessionPlanCommand(
            id,
            body.Title,
            body.PreSessionNote,
            body.SessionObjective,
            body.SessionNotes,
            body.AgendaItemsJson,
            body.PostSessionSummary,
            body.LinkedAssignmentId);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Oturum plani sil</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteSessionPlanCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Oturum planini paylas</summary>
    [HttpPost("{id:guid}/share")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Share(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ShareSessionPlanCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Oturum planini tamamla</summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CompleteSessionPlanCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Oturum planina materyal ekle</summary>
    [HttpPost("{id:guid}/materials")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMaterial(Guid id, [FromBody] AddSessionPlanMaterialRequest body, CancellationToken ct)
    {
        var command = new AddSessionPlanMaterialCommand(id, body.LibraryItemId, body.Phase, body.Note);
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id }, new { materialId = result.Data });
    }

    /// <summary>Oturum planindan materyal kaldir</summary>
    [HttpDelete("{id:guid}/materials/{materialId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveMaterial(Guid id, Guid materialId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveSessionPlanMaterialCommand(materialId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Oturum planini sablon olarak kaydet</summary>
    [HttpPost("{id:guid}/save-as-template")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveAsTemplate(Guid id, [FromBody] SaveAsTemplateRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SaveSessionPlanAsTemplateCommand(id, body.TemplateName), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Sablondan oturum plani olustur</summary>
    [HttpPost("from-template/{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromTemplate(Guid templateId, [FromBody] CreateFromTemplateRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSessionPlanFromTemplateCommand(templateId, body.NewTitle, body.BookingId, body.GroupClassId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Oturum plani sablonlarimi listele</summary>
    [HttpGet("templates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTemplates(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMySessionPlanTemplatesQuery(search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Booking'e ait oturum plani</summary>
    [HttpGet("booking/{bookingId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBooking(Guid bookingId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionPlanByBookingQuery(bookingId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Grup dersine ait oturum plani</summary>
    [HttpGet("group-class/{classId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGroupClass(Guid classId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSessionPlanByGroupClassQuery(classId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }
}

public record UpdateSessionPlanRequest(
    string? Title,
    string? PreSessionNote,
    string? SessionObjective,
    string? SessionNotes,
    string? AgendaItemsJson,
    string? PostSessionSummary,
    Guid? LinkedAssignmentId);

public record AddSessionPlanMaterialRequest(
    Guid LibraryItemId,
    SessionPhase Phase,
    string? Note);

public record SaveAsTemplateRequest(string TemplateName);

public record CreateFromTemplateRequest(
    string? NewTitle,
    Guid? BookingId,
    Guid? GroupClassId);
