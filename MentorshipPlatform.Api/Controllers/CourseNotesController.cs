using MediatR;
using MentorshipPlatform.Application.Courses.Commands.CreateLectureNote;
using MentorshipPlatform.Application.Courses.Commands.UpdateLectureNote;
using MentorshipPlatform.Application.Courses.Commands.DeleteLectureNote;
using MentorshipPlatform.Application.Courses.Queries.GetLectureNotes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/course-notes")]
[Authorize]
public class CourseNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CourseNotesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Derse ait notları getir</summary>
    [HttpGet("lecture/{lectureId:guid}")]
    public async Task<IActionResult> GetLectureNotes(Guid lectureId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLectureNotesQuery(lectureId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Yeni not oluştur</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateLectureNoteCommand(body.LectureId, body.TimestampSec, body.Content), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"/api/course-notes/{result.Data}", new { id = result.Data });
    }

    /// <summary>Not güncelle</summary>
    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> Update(Guid noteId, [FromBody] UpdateNoteRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateLectureNoteCommand(noteId, body.Content, body.TimestampSec), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Not sil</summary>
    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid noteId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteLectureNoteCommand(noteId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }
}

// Request DTOs
public record CreateNoteRequest(Guid LectureId, int TimestampSec, string Content);
public record UpdateNoteRequest(string Content, int? TimestampSec);
