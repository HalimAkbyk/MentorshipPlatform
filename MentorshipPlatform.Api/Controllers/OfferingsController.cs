using System.Text.RegularExpressions;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Offerings.Commands.CreateOffering;
using MentorshipPlatform.Application.Offerings.Commands.DeleteOffering;
using MentorshipPlatform.Application.Offerings.Commands.ReorderOfferings;
using MentorshipPlatform.Application.Offerings.Commands.ToggleOffering;
using MentorshipPlatform.Application.Offerings.Commands.UpdateOffering;
using MentorshipPlatform.Application.Offerings.Commands.UpsertBookingQuestions;
using MentorshipPlatform.Application.Offerings.Queries.GetMentorOfferings;
using MentorshipPlatform.Application.Offerings.Queries.GetMyOfferings;
using MentorshipPlatform.Application.Offerings.Queries.GetOfferingById;
using MentorshipPlatform.Application.Availability.Commands.SaveOfferingAvailabilityTemplate;
using MentorshipPlatform.Application.Availability.Commands.DeleteOfferingAvailabilityTemplate;
using MentorshipPlatform.Application.Availability.Queries.GetOfferingAvailabilityTemplate;
using MentorshipPlatform.Application.Availability.Commands.SaveAvailabilityTemplate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/offerings")]
public class OfferingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storageService;

    public OfferingsController(
        IMediator mediator,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IStorageService storageService)
    {
        _mediator = mediator;
        _context = context;
        _currentUser = currentUser;
        _storageService = storageService;
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
            body.CoverImageUrl,
            body.CoverImagePosition,
            body.CoverImageTransform);

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
    // ---- Offering-level Availability Template ----

    /// <summary>Paketin müsaitlik programını getir (özel veya varsayılan)</summary>
    [HttpGet("{id:guid}/availability-template")]
    [Authorize(Roles = "Mentor")]
    public async Task<IActionResult> GetOfferingAvailabilityTemplate(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOfferingAvailabilityTemplateQuery(id), ct);
        if (!result.IsSuccess)
        {
            if (result.Errors.Any(e => e.ToLower().Contains("not found")))
                return NotFound(new { errors = result.Errors });
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(result.Data);
    }

    /// <summary>Paket için özel müsaitlik programı kaydet (oluştur veya güncelle)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPut("{id:guid}/availability-template")]
    public async Task<IActionResult> SaveOfferingAvailabilityTemplate(
        Guid id,
        [FromBody] SaveOfferingTemplateRequest body,
        CancellationToken ct)
    {
        var command = new SaveOfferingAvailabilityTemplateCommand(
            id,
            body.Name,
            body.Timezone,
            body.Rules.Select(r => new AvailabilityRuleDto(
                r.DayOfWeek, r.IsActive, r.StartTime, r.EndTime, r.SlotIndex ?? 0)).ToList(),
            body.Settings != null ? new AvailabilitySettingsDto(
                body.Settings.MinNoticeHours,
                body.Settings.MaxBookingDaysAhead,
                body.Settings.BufferAfterMin,
                body.Settings.SlotGranularityMin,
                body.Settings.MaxBookingsPerDay) : null);

        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { templateId = result.Data });
    }

    /// <summary>Paketin özel müsaitlik programını sil (varsayılana dön)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpDelete("{id:guid}/availability-template")]
    public async Task<IActionResult> DeleteOfferingAvailabilityTemplate(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteOfferingAvailabilityTemplateCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }
    /// <summary>Paket kapak görseli yükle</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost("{id:guid}/upload-cover")]
    [RequestSizeLimit(10_485_760)] // 10 MB
    public async Task<IActionResult> UploadCoverImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Dosya seçilmedi" } });

        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == id && o.MentorUserId == userId.Value, ct);

        if (offering == null)
            return NotFound(new { errors = new[] { "Paket bulunamadı" } });

        var sanitizedFileName = SanitizeFileName(file.FileName);

        using var stream = file.OpenReadStream();
        var uploadResult = await _storageService.UploadFileAsync(
            stream, sanitizedFileName, file.ContentType, userId.ToString()!, "offering-cover", ct);

        if (!uploadResult.Success)
            return BadRequest(new { errors = new[] { uploadResult.ErrorMessage ?? "Kapak görseli yükleme başarısız" } });

        offering.Update(
            offering.Title, offering.Description, offering.DurationMinDefault,
            offering.PriceAmount, offering.Category, offering.Subtitle,
            offering.DetailedDescription, offering.SessionType,
            offering.MaxBookingDaysAhead, offering.MinNoticeHours,
            uploadResult.PublicUrl, offering.CoverImagePosition, offering.CoverImageTransform);
        await _context.SaveChangesAsync(ct);

        return Ok(new { coverImageUrl = uploadResult.PublicUrl });
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "image.jpg";
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(fileName);
        name = name.Replace("ğ", "g").Replace("Ğ", "G")
            .Replace("ü", "u").Replace("Ü", "U")
            .Replace("ş", "s").Replace("Ş", "S")
            .Replace("ı", "i").Replace("İ", "I")
            .Replace("ö", "o").Replace("Ö", "O")
            .Replace("ç", "c").Replace("Ç", "C");
        var sanitized = new string(name.Where(c => c < 128)
            .Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '-').ToArray());
        sanitized = Regex.Replace(sanitized, @"-+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "image";
        return sanitized + ext;
    }
}

// Request DTOs
public record SaveOfferingTemplateRuleRequest(
    int DayOfWeek,
    bool IsActive,
    string? StartTime,
    string? EndTime,
    int? SlotIndex);

public record SaveOfferingTemplateSettingsRequest(
    int? MinNoticeHours,
    int? MaxBookingDaysAhead,
    int? BufferAfterMin,
    int? SlotGranularityMin,
    int? MaxBookingsPerDay);

public record SaveOfferingTemplateRequest(
    string? Name,
    string? Timezone,
    List<SaveOfferingTemplateRuleRequest> Rules,
    SaveOfferingTemplateSettingsRequest? Settings);
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
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform);

public record ReorderOfferingsRequest(List<Guid> OfferingIds);

public record QuestionRequest(string QuestionText, bool IsRequired);
public record UpsertQuestionsRequest(List<QuestionRequest> Questions);
