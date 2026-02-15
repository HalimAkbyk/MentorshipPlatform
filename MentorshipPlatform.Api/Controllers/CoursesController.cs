using MediatR;
using MentorshipPlatform.Application.Courses.Commands.CreateCourse;
using MentorshipPlatform.Application.Courses.Commands.UpdateCourse;
using MentorshipPlatform.Application.Courses.Commands.PublishCourse;
using MentorshipPlatform.Application.Courses.Commands.ArchiveCourse;
using MentorshipPlatform.Application.Courses.Commands.DeleteCourse;
using MentorshipPlatform.Application.Courses.Queries.GetMyCourses;
using MentorshipPlatform.Application.Courses.Queries.GetCourseForEdit;
using MentorshipPlatform.Application.Courses.Queries.GetPublicCourses;
using MentorshipPlatform.Application.Courses.Queries.GetCourseDetail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoursesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Mentor'un kendi kurslarını listele</summary>
    [Authorize(Roles = "Mentor")]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyCourses(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyCoursesQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs düzenleme detayı (mentor)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> GetCourseForEdit(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCourseForEditQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Yeni kurs oluştur</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetCourseDetail), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Kurs güncelle</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest body, CancellationToken ct)
    {
        var command = new UpdateCourseCommand(
            id,
            body.Title,
            body.ShortDescription,
            body.Description,
            body.Price,
            body.Category,
            body.Language,
            body.Level,
            body.CoverImageUrl,
            body.PromoVideoKey,
            body.WhatYouWillLearn,
            body.Requirements,
            body.TargetAudience);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kursu yayınla</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishCourseCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kursu arşivle</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ArchiveCourseCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kursu sil (sadece Draft)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCourseCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs kataloğu (herkese açık)</summary>
    [AllowAnonymous]
    [HttpGet("catalog")]
    public async Task<IActionResult> GetPublicCourses(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? level,
        [FromQuery] string? sortBy,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPublicCoursesQuery(search, category, level, sortBy, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs detayı (herkese açık)</summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCourseDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCourseDetailQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }
}

// Request DTOs
public record UpdateCourseRequest(
    string Title,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string? Category,
    string? Language,
    string? Level,
    string? CoverImageUrl,
    string? PromoVideoKey,
    List<string>? WhatYouWillLearn,
    List<string>? Requirements,
    List<string>? TargetAudience);
