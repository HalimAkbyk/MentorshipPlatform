using MediatR;
using MentorshipPlatform.Application.Assignments.Commands.AddAssignmentMaterial;
using MentorshipPlatform.Application.Assignments.Commands.CloseAssignment;
using MentorshipPlatform.Application.Assignments.Commands.CreateAssignment;
using MentorshipPlatform.Application.Assignments.Commands.DeleteAssignment;
using MentorshipPlatform.Application.Assignments.Commands.PublishAssignment;
using MentorshipPlatform.Application.Assignments.Commands.RemoveAssignmentMaterial;
using MentorshipPlatform.Application.Assignments.Commands.ReviewSubmission;
using MentorshipPlatform.Application.Assignments.Commands.SubmitAssignment;
using MentorshipPlatform.Application.Assignments.Commands.CreateFromTemplate;
using MentorshipPlatform.Application.Assignments.Commands.SaveAsTemplate;
using MentorshipPlatform.Application.Assignments.Commands.UpdateAssignment;
using MentorshipPlatform.Application.Assignments.Queries.GetAssignmentById;
using MentorshipPlatform.Application.Assignments.Queries.GetAssignmentSubmissions;
using MentorshipPlatform.Application.Assignments.Queries.GetMyAssignments;
using MentorshipPlatform.Application.Assignments.Queries.GetMyTemplates;
using MentorshipPlatform.Application.Assignments.Queries.GetStudentAssignments;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssignmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Yeni odev olustur</summary>
    [HttpPost]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Odevlerimi listele (Mentor)</summary>
    [HttpGet]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAssignments(
        [FromQuery] AssignmentStatus? status,
        [FromQuery] AssignmentType? type,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMyAssignmentsQuery(status, type, search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odev detayi</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAssignmentByIdQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Odev guncelle</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssignmentRequest body, CancellationToken ct)
    {
        var command = new UpdateAssignmentCommand(
            id,
            body.Title,
            body.Description,
            body.Instructions,
            body.AssignmentType,
            body.DifficultyLevel,
            body.EstimatedMinutes,
            body.DueDate,
            body.MaxScore,
            body.AllowLateSubmission,
            body.LatePenaltyPercent,
            body.BookingId,
            body.GroupClassId,
            body.CurriculumTopicId);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odev sil</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteAssignmentCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odev yayinla</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishAssignmentCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odev kapat</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseAssignmentCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odeve materyal ekle</summary>
    [HttpPost("{id:guid}/materials")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMaterial(Guid id, [FromBody] AddMaterialRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddAssignmentMaterialCommand(id, body.LibraryItemId, body.IsRequired), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created("", new { id = result.Data });
    }

    /// <summary>Odevden materyal kaldir</summary>
    [HttpDelete("{id:guid}/materials/{itemId:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveMaterial(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveAssignmentMaterialCommand(id, itemId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odev teslim et (Ogrenci)</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "RequireStudentRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitAssignmentCommand(id, body.SubmissionText, body.FileUrl, body.OriginalFileName), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created("", new { id = result.Data });
    }

    /// <summary>Odev teslimlerini listele (Mentor)</summary>
    [HttpGet("{id:guid}/submissions")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSubmissions(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAssignmentSubmissionsQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Teslimi degerlendir (Mentor)</summary>
    [HttpPost("submissions/{subId:guid}/review")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewSubmission(Guid subId, [FromBody] ReviewRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReviewSubmissionCommand(subId, body.Score, body.Feedback, body.ReviewStatus), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created("", new { id = result.Data });
    }

    /// <summary>Ogrenci odevlerim</summary>
    [HttpGet("student/me")]
    [Authorize(Policy = "RequireStudentRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentAssignments(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStudentAssignmentsQuery(search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Odevi sablon olarak kaydet</summary>
    [HttpPost("{id:guid}/save-as-template")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveAsTemplate(Guid id, [FromBody] AssignmentSaveAsTemplateRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SaveAssignmentAsTemplateCommand(id, body.TemplateName), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Sablondan odev olustur</summary>
    [HttpPost("from-template/{templateId:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromTemplate(Guid templateId, [FromBody] AssignmentCreateFromTemplateRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateAssignmentFromTemplateCommand(
            templateId, body.NewTitle, body.BookingId, body.GroupClassId, body.CurriculumTopicId, body.DueDate), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Odev sablonlarimi listele</summary>
    [HttpGet("templates")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTemplates(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMyAssignmentTemplatesQuery(search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }
}

public record UpdateAssignmentRequest(
    string Title,
    string? Description,
    string? Instructions,
    AssignmentType AssignmentType,
    DifficultyLevel? DifficultyLevel,
    int? EstimatedMinutes,
    DateTime? DueDate,
    int? MaxScore,
    bool AllowLateSubmission,
    int? LatePenaltyPercent,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId);

public record AddMaterialRequest(Guid LibraryItemId, bool IsRequired = true);

public record SubmitRequest(string? SubmissionText, string? FileUrl, string? OriginalFileName);

public record ReviewRequest(int? Score, string? Feedback, ReviewStatus ReviewStatus);

public record AssignmentSaveAsTemplateRequest(string TemplateName);

public record AssignmentCreateFromTemplateRequest(
    string? NewTitle,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId,
    DateTime? DueDate);
