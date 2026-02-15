using MediatR;
using MentorshipPlatform.Application.Courses.Commands.CreateSection;
using MentorshipPlatform.Application.Courses.Commands.UpdateSection;
using MentorshipPlatform.Application.Courses.Commands.DeleteSection;
using MentorshipPlatform.Application.Courses.Commands.ReorderSections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/sections")]
[Authorize(Roles = "Mentor")]
public class CourseSectionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CourseSectionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Yeni bölüm ekle</summary>
    [HttpPost]
    public async Task<IActionResult> Create(Guid courseId, [FromBody] CreateSectionRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSectionCommand(courseId, body.Title), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Created($"/api/courses/{courseId}/sections/{result.Data}", new { id = result.Data });
    }

    /// <summary>Bölüm güncelle</summary>
    [HttpPut("{sectionId:guid}")]
    public async Task<IActionResult> Update(Guid courseId, Guid sectionId, [FromBody] UpdateSectionRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateSectionCommand(sectionId, body.Title), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Bölüm sil</summary>
    [HttpDelete("{sectionId:guid}")]
    public async Task<IActionResult> Delete(Guid courseId, Guid sectionId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteSectionCommand(sectionId), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Bölüm sıralamasını değiştir</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(Guid courseId, [FromBody] ReorderSectionsRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderSectionsCommand(courseId, body.SectionIds), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }
}

// Request DTOs
public record CreateSectionRequest(string Title);
public record UpdateSectionRequest(string Title);
public record ReorderSectionsRequest(List<Guid> SectionIds);
