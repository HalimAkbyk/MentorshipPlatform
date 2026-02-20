using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Courses.Commands.EnrollInCourse;
using MentorshipPlatform.Application.Courses.Commands.UpdateLectureProgress;
using MentorshipPlatform.Application.Courses.Commands.CompleteLecture;
using MentorshipPlatform.Application.Courses.Queries.GetEnrolledCourses;
using MentorshipPlatform.Application.Courses.Queries.GetCoursePlayer;
using MentorshipPlatform.Application.Courses.Queries.GetCourseProgress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/course-enrollments")]
[Authorize]
public class CourseEnrollmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagService _featureFlags;

    public CourseEnrollmentsController(IMediator mediator, IFeatureFlagService featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    /// <summary>Kursa kayıt ol</summary>
    [HttpPost("{courseId:guid}")]
    public async Task<IActionResult> Enroll(Guid courseId, CancellationToken ct)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlags.CourseSalesEnabled, ct))
            return BadRequest(new { errors = new[] { "Kurs satislari gecici olarak durdurulmustur." } });

        var result = await _mediator.Send(new EnrollInCourseCommand(courseId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"/api/course-enrollments/{courseId}", new { enrollmentId = result.Data });
    }

    /// <summary>Kayıtlı kurslarım</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetEnrolledCourses([FromQuery] int page = 1, [FromQuery] int pageSize = 15, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEnrolledCoursesQuery(page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs oynatıcı (video + müfredat + ilerleme)</summary>
    [HttpGet("{courseId:guid}/player")]
    public async Task<IActionResult> GetCoursePlayer(Guid courseId, [FromQuery] Guid? lectureId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCoursePlayerQuery(courseId, lectureId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Ders ilerleme güncelle</summary>
    [HttpPost("progress/{lectureId:guid}")]
    public async Task<IActionResult> UpdateProgress(Guid lectureId, [FromBody] UpdateProgressRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateLectureProgressCommand(lectureId, body.WatchedSec, body.LastPositionSec), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Dersi tamamla</summary>
    [HttpPost("complete/{lectureId:guid}")]
    public async Task<IActionResult> CompleteLecture(Guid lectureId, CancellationToken ct)
    {
        var result = await _mediator.Send(new CompleteLectureCommand(lectureId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs ilerleme detayı</summary>
    [HttpGet("{courseId:guid}/progress")]
    public async Task<IActionResult> GetCourseProgress(Guid courseId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCourseProgressQuery(courseId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }
}

// Request DTOs
public record UpdateProgressRequest(int WatchedSec, int LastPositionSec);
