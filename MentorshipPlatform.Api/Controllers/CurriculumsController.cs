using MediatR;
using MentorshipPlatform.Application.Curriculum.Commands.CreateCurriculum;
using MentorshipPlatform.Application.Curriculum.Commands.UpdateCurriculum;
using MentorshipPlatform.Application.Curriculum.Commands.DeleteCurriculum;
using MentorshipPlatform.Application.Curriculum.Commands.PublishCurriculum;
using MentorshipPlatform.Application.Curriculum.Commands.AddCurriculumWeek;
using MentorshipPlatform.Application.Curriculum.Commands.UpdateCurriculumWeek;
using MentorshipPlatform.Application.Curriculum.Commands.DeleteCurriculumWeek;
using MentorshipPlatform.Application.Curriculum.Commands.AddCurriculumTopic;
using MentorshipPlatform.Application.Curriculum.Commands.UpdateCurriculumTopic;
using MentorshipPlatform.Application.Curriculum.Commands.DeleteCurriculumTopic;
using MentorshipPlatform.Application.Curriculum.Commands.AddTopicMaterial;
using MentorshipPlatform.Application.Curriculum.Commands.RemoveTopicMaterial;
using MentorshipPlatform.Application.Curriculum.Commands.AssignCurriculumToStudent;
using MentorshipPlatform.Application.Curriculum.Queries.GetMyCurriculums;
using MentorshipPlatform.Application.Curriculum.Queries.GetCurriculumById;
using MentorshipPlatform.Application.Curriculum.Queries.GetStudentCurriculumProgress;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/curriculums")]
public class CurriculumsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CurriculumsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Yeni mufredat olustur</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCurriculumCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Mufredatlarimi listele</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCurriculums(
        [FromQuery] CurriculumStatus? status,
        [FromQuery] string? subject,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMyCurriculumsQuery(status, subject, search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Mufredat detayi</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCurriculumByIdQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Mufredat guncelle</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCurriculumRequest body, CancellationToken ct)
    {
        var command = new UpdateCurriculumCommand(
            id,
            body.Title,
            body.Description,
            body.Subject,
            body.Level,
            body.TotalWeeks,
            body.EstimatedHoursPerWeek,
            body.CoverImageUrl,
            body.IsDefault);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Mufredat sil (arsivle)</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCurriculumCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Mufredati yayinla</summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishCurriculumCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Mufredatta hafta ekle</summary>
    [HttpPost("{id:guid}/weeks")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddWeek(Guid id, [FromBody] AddWeekRequest body, CancellationToken ct)
    {
        var command = new AddCurriculumWeekCommand(id, body.Title, body.Description);
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"api/curriculums/weeks/{result.Data}", new { id = result.Data });
    }

    /// <summary>Hafta guncelle</summary>
    [HttpPut("weeks/{weekId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateWeek(Guid weekId, [FromBody] UpdateWeekRequest body, CancellationToken ct)
    {
        var command = new UpdateCurriculumWeekCommand(weekId, body.Title, body.Description);
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Hafta sil</summary>
    [HttpDelete("weeks/{weekId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteWeek(Guid weekId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCurriculumWeekCommand(weekId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Haftaya konu ekle</summary>
    [HttpPost("weeks/{weekId:guid}/topics")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddTopic(Guid weekId, [FromBody] AddTopicRequest body, CancellationToken ct)
    {
        var command = new AddCurriculumTopicCommand(weekId, body.Title, body.Description, body.EstimatedMinutes, body.ObjectiveText);
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"api/curriculums/topics/{result.Data}", new { id = result.Data });
    }

    /// <summary>Konu guncelle</summary>
    [HttpPut("topics/{topicId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTopic(Guid topicId, [FromBody] UpdateTopicRequest body, CancellationToken ct)
    {
        var command = new UpdateCurriculumTopicCommand(
            topicId, body.Title, body.Description, body.EstimatedMinutes,
            body.ObjectiveText, body.LinkedExamId, body.LinkedAssignmentId);
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Konu sil</summary>
    [HttpDelete("topics/{topicId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTopic(Guid topicId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCurriculumTopicCommand(topicId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Konuya materyal ekle</summary>
    [HttpPost("topics/{topicId:guid}/materials")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMaterial(Guid topicId, [FromBody] AddTopicMaterialRequest body, CancellationToken ct)
    {
        var command = new AddTopicMaterialCommand(topicId, body.LibraryItemId, body.MaterialRole ?? "Primary");
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"api/curriculums/topics/{topicId}/materials/{body.LibraryItemId}", new { id = result.Data });
    }

    /// <summary>Konudan materyal kaldir</summary>
    [HttpDelete("topics/{topicId:guid}/materials/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveMaterial(Guid topicId, Guid itemId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RemoveTopicMaterialCommand(topicId, itemId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Mufredati ogrenciye ata</summary>
    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignToStudent(Guid id, [FromBody] AssignStudentRequest body, CancellationToken ct)
    {
        var command = new AssignCurriculumToStudentCommand(id, body.StudentUserId);
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"api/curriculums/{id}/progress/{body.StudentUserId}", new { enrollmentId = result.Data });
    }

    /// <summary>Ogrenci mufredat ilerlemesi</summary>
    [HttpGet("{id:guid}/progress/{studentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentProgress(Guid id, Guid studentId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStudentCurriculumProgressQuery(id, studentId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }
}

// Request DTOs
public record UpdateCurriculumRequest(
    string Title,
    string? Description,
    string? Subject,
    string? Level,
    int TotalWeeks,
    int? EstimatedHoursPerWeek,
    string? CoverImageUrl,
    bool IsDefault);

public record AddWeekRequest(string Title, string? Description);

public record UpdateWeekRequest(string Title, string? Description);

public record AddTopicRequest(
    string Title,
    string? Description,
    int? EstimatedMinutes,
    string? ObjectiveText);

public record UpdateTopicRequest(
    string Title,
    string? Description,
    int? EstimatedMinutes,
    string? ObjectiveText,
    Guid? LinkedExamId,
    Guid? LinkedAssignmentId);

public record AddTopicMaterialRequest(Guid LibraryItemId, string? MaterialRole);

public record AssignStudentRequest(Guid StudentUserId);
