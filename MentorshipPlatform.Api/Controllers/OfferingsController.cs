using MediatR;
using MentorshipPlatform.Application.Offerings.Commands.CreateOffering;
using MentorshipPlatform.Application.Offerings.Commands.DeleteOffering;
using MentorshipPlatform.Application.Offerings.Commands.ReorderOfferings;
using MentorshipPlatform.Application.Offerings.Commands.ToggleOffering;
using MentorshipPlatform.Application.Offerings.Commands.UpdateOffering;
using MentorshipPlatform.Application.Offerings.Commands.UpsertBookingQuestions;
using MentorshipPlatform.Application.Offerings.Queries.GetMentorOfferings;
using MentorshipPlatform.Application.Offerings.Queries.GetMyOfferings;
using MentorshipPlatform.Application.Offerings.Queries.GetOfferingById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/offerings")]
public class OfferingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OfferingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Mentor'un kendi paketlerini listele</summary>
    [Authorize(Roles = "Mentor")]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyOfferings(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyOfferingsQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Paket detayı (public)</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOfferingByIdQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Bir mentor'un aktif paketleri (public, öğrenci görünümü)</summary>
    [HttpGet("mentor/{mentorId:guid}")]
    public async Task<IActionResult> GetMentorOfferings(Guid mentorId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMentorOfferingsQuery(mentorId), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Yeni paket oluştur</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOfferingCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Paket güncelle</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferingRequest body, CancellationToken ct)
    {
        var command = new UpdateOfferingCommand(
            id,
            body.Title,
            body.Description,
            body.DurationMin,
            body.Price,
            body.Category,
            body.Subtitle,
            body.DetailedDescription,
            body.SessionType,
            body.MaxBookingDaysAhead,
            body.MinNoticeHours,
            body.CoverImageUrl);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Paket sil</summary>
    [Authorize(Roles = "Mentor")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteOfferingCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Paket aktif/pasif toggle</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ToggleOfferingCommand(id), ct);
        return result.IsSuccess ? Ok(new { isActive = result.Data }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Paket sıralamasını değiştir</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderOfferingsRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReorderOfferingsCommand(body.OfferingIds), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Paket sorularını güncelle (max 4)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPut("{id:guid}/questions")]
    public async Task<IActionResult> UpsertQuestions(Guid id, [FromBody] UpsertQuestionsRequest body, CancellationToken ct)
    {
        var questions = body.Questions.Select(q => new BookingQuestionDto(q.QuestionText, q.IsRequired)).ToList();
        var result = await _mediator.Send(new UpsertBookingQuestionsCommand(id, questions), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }
}

// Request DTOs
public record UpdateOfferingRequest(
    string Title,
    string? Description,
    int DurationMin,
    decimal Price,
    string? Category,
    string? Subtitle,
    string? DetailedDescription,
    string? SessionType,
    int MaxBookingDaysAhead,
    int MinNoticeHours,
    string? CoverImageUrl);

public record ReorderOfferingsRequest(List<Guid> OfferingIds);

public record QuestionRequest(string QuestionText, bool IsRequired);
public record UpsertQuestionsRequest(List<QuestionRequest> Questions);
