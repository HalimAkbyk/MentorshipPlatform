using MediatR;
using MentorshipPlatform.Application.Courses.Commands.AdminReviewCourse;
using MentorshipPlatform.Application.Courses.Queries.GetCourseReviewDetail;
using MentorshipPlatform.Application.Courses.Queries.GetPendingReviewCourses;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/course-reviews")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminCourseReviewController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminCourseReviewController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>İnceleme bekleyen kursları listele</summary>
    [HttpGet]
    public async Task<IActionResult> GetPendingReviewCourses(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPendingReviewCoursesQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs inceleme detayı (tüm videolar, geçmiş roundlar)</summary>
    [HttpGet("{courseId:guid}")]
    public async Task<IActionResult> GetCourseReviewDetail(Guid courseId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCourseReviewDetailQuery(courseId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Kurs incelemesi yap (Onayla / Reddet / Revizyon İste)</summary>
    [HttpPost("{courseId:guid}")]
    public async Task<IActionResult> ReviewCourse(
        Guid courseId,
        [FromBody] AdminReviewCourseRequest body,
        CancellationToken ct)
    {
        var command = new AdminReviewCourseCommand(
            courseId,
            body.Outcome,
            body.GeneralNotes,
            body.LectureComments ?? new List<LectureCommentInput>());

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }
}

// Request DTO
public record AdminReviewCourseRequest(
    ReviewOutcome Outcome,
    string? GeneralNotes,
    List<LectureCommentInput>? LectureComments);
