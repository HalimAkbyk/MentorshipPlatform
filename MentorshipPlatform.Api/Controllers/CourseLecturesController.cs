using MediatR;
using MentorshipPlatform.Application.Courses.Commands.CreateLecture;
using MentorshipPlatform.Application.Courses.Commands.UpdateLecture;
using MentorshipPlatform.Application.Courses.Commands.DeleteLecture;
using MentorshipPlatform.Application.Courses.Commands.ReorderLectures;
using MentorshipPlatform.Application.Courses.Commands.GetVideoUploadUrl;
using MentorshipPlatform.Application.Courses.Commands.ConfirmVideoUpload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Authorize(Roles = "Mentor")]
public class CourseLecturesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CourseLecturesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Yeni ders ekle</summary>
    [HttpPost("api/sections/{sectionId:guid}/lectures")]
    public async Task<IActionResult> Create(Guid sectionId, [FromBody] CreateLectureRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateLectureCommand(sectionId, body.Title, body.Type, body.IsPreview, body.Description), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"/api/lectures/{result.Data}", new { id = result.Data });
    }

    /// <summary>Ders güncelle</summary>
    [HttpPut("api/sections/{sectionId:guid}/lectures/{lectureId:guid}")]
    public async Task<IActionResult> Update(Guid sectionId, Guid lectureId, [FromBody] UpdateLectureRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateLectureCommand(lectureId, body.Title, body.Description, body.IsPreview, body.TextContent), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Ders sil</summary>
    [HttpDelete("api/sections/{sectionId:guid}/lectures/{lectureId:guid}")]
    public async Task<IActionResult> Delete(Guid sectionId, Guid lectureId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteLectureCommand(lectureId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Ders sıralamasını değiştir</summary>
    [HttpPut("api/sections/{sectionId:guid}/lectures/reorder")]
    public async Task<IActionResult> Reorder(Guid sectionId, [FromBody] ReorderLecturesRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderLecturesCommand(sectionId, body.LectureIds), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Video yükleme URL'si al (presigned PUT)</summary>
    [HttpPost("api/lectures/{lectureId:guid}/upload-url")]
    public async Task<IActionResult> GetUploadUrl(Guid lectureId, [FromBody] GetUploadUrlRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetVideoUploadUrlCommand(lectureId, body.FileName, body.ContentType), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Video yükleme onayı</summary>
    [HttpPost("api/lectures/{lectureId:guid}/confirm-upload")]
    public async Task<IActionResult> ConfirmUpload(Guid lectureId, [FromBody] ConfirmUploadRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ConfirmVideoUploadCommand(lectureId, body.VideoKey, body.DurationSec), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }
}

// Request DTOs
public record CreateLectureRequest(string Title, string? Type, bool IsPreview, string? Description);
public record UpdateLectureRequest(string Title, string? Description, bool IsPreview, string? TextContent);
public record ReorderLecturesRequest(List<Guid> LectureIds);
public record GetUploadUrlRequest(string FileName, string ContentType);
public record ConfirmUploadRequest(string VideoKey, int DurationSec);
