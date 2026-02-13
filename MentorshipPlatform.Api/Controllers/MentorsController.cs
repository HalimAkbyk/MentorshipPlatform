using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Application.Availability.Commands.AddAvailabilitySlot;
using MentorshipPlatform.Application.Availability.Commands.AddAvailabilityOverride;
using MentorshipPlatform.Application.Availability.Commands.DeleteAvailabilitySlot;
using MentorshipPlatform.Application.Availability.Commands.SaveAvailabilityTemplate;
using MentorshipPlatform.Application.Availability.Queries.GetAvailabilityTemplate;
using MentorshipPlatform.Application.Availability.Queries.GetAvailableTimeSlots;
using MentorshipPlatform.Application.Availability.Queries.GetMentorAvailability;
using MentorshipPlatform.Application.Availability.Queries.GetMyAvailability;
using MentorshipPlatform.Application.Mentors.Commands.CreateMentorProfile;
using MentorshipPlatform.Application.Mentors.Commands.DeleteVerification;
using MentorshipPlatform.Application.Mentors.Commands.SubmitVerification;
using MentorshipPlatform.Application.Mentors.Commands.UpdateMentorProfile;
using MentorshipPlatform.Application.Mentors.Queries.GetMentorById;
using MentorshipPlatform.Application.Mentors.Queries.GetMentorsList;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/mentors")]
public class MentorsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MentorsController(IMediator mediator, IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<MentorListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentors([FromQuery] GetMentorsListQuery query)
    {
        var result = await _mediator.Send(query);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MentorDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMentorById(Guid id)
    {
        var result = await _mediator.Send(new GetMentorByIdQuery(id));
        if (!result.IsSuccess)
        {
            // “Mentor not found” gibi durumda 404 daha mantıklı
            if (result.Errors.Any(e => e.ToLower().Contains("not found")))
                return NotFound(new { errors = result.Errors });

            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    // ---- ME (mentor) profile (onboarding step 1)

    [HttpGet("me/profile")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var id = _currentUser.UserId.Value;

        var profile = await _db.MentorProfiles
            .Include(x => x.Verifications)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == id, ct);

        if (profile == null) return NotFound(new { errors = new[] { "Mentor profile not found" } });

        // ✅ En az bir onaylı verification varsa mentor booking kabul edebilir
        var isApprovedForBookings = profile.Verifications.Any(v => v.Status == VerificationStatus.Approved);

        // ✅ Verification URL'lerini presigned URL'e çevir (storage service'den)
        var verificationsWithUrls = new List<object>();
        foreach (var v in profile.Verifications)
        {
            string? documentUrl = v.DocumentUrl;

            // Eğer documentUrl bir fileKey ise (GUID içeriyorsa), presigned URL al
            if (!string.IsNullOrEmpty(documentUrl))
            {
                // URL zaten presigned ise (? işareti içeriyorsa) olduğu gibi bırak
                // Değilse yeni presigned URL oluştur
                if (!documentUrl.Contains("?"))
                {
                    try
                    {
                        // Sadece fileKey'i çıkar (endpoint/bucket kısmını at)
                        var parts = documentUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var fileKey = string.Join("/", parts.Skip(parts.Length - 2));
                            var storageService = HttpContext.RequestServices.GetRequiredService<IStorageService>();
                            documentUrl = await storageService.GetPresignedUrlAsync(fileKey, TimeSpan.FromDays(7), ct);
                        }
                    }
                    catch
                    {
                        // URL oluşturulamazsa eski URL'i kullan
                    }
                }
            }

            verificationsWithUrls.Add(new
            {
                id = v.Id,
                type = v.Type.ToString(),
                status = v.Status.ToString(),
                documentUrl = documentUrl,
                reviewedAt = v.ReviewedAt,
                notes = v.Notes
            });
        }

        return Ok(new
        {
            university = profile.University,
            department = profile.Department,
            bio = profile.Bio,
            graduationYear = profile.GraduationYear,
            headline = profile.Headline,
            isListed = profile.IsListed,
            isApprovedForBookings = isApprovedForBookings,
            verifications = verificationsWithUrls
        });
    }

    [HttpPost("me/profile")]
    [Authorize(Policy = "RequireMentorRole")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateMentorProfileCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { mentorUserId = result.Data });
    }

    [HttpPatch("me/profile")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateMentorProfileCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { ok = true });
    }

    [HttpPost("me/verification")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> SubmitVerification([FromForm] SubmitVerificationCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { verificationId = result.Data });
    }

    [HttpDelete("me/verification/{verificationId}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> DeleteVerification(Guid verificationId)
    {
        var result = await _mediator.Send(new DeleteVerificationCommand(verificationId));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { success = result.Data });
    }

    // ---- ME (mentor) offerings (onboarding step 2)

    public sealed record UpsertOfferingsRequest(decimal HourlyRate, int DurationMinDefault);

    [HttpGet("me/offerings")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetMyOfferings(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();

        var id = _currentUser.UserId.Value;

        var list = await _db.Offerings
            .AsNoTracking()
            .Where(o => o.MentorUserId == id && o.IsActive)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                id = o.Id,
                type = o.Type.ToString(),
                title = o.Title,
                durationMinDefault = o.DurationMinDefault,
                priceAmount = o.PriceAmount,
                currency = o.Currency,
                isActive = o.IsActive,
            })
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPut("me/offerings")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> UpsertMyOfferings([FromBody] UpsertOfferingsRequest req, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var id = _currentUser.UserId.Value;

        // profile yoksa onboarding ilerlemesin
        var profileExists = await _db.MentorProfiles.AnyAsync(x => x.UserId == id, ct);
        if (!profileExists) return BadRequest(new { errors = new[] { "Mentor profile required before offerings" } });

        // one-to-one offering'i upsert edelim
        var offering = await _db.Offerings
            .FirstOrDefaultAsync(o => o.MentorUserId == id && o.Type == OfferingType.OneToOne, ct);

        if (offering == null)
        {
            offering = Offering.Create(
                mentorUserId: id,
                type: OfferingType.OneToOne,
                title: "Birebir Mentorluk",
                durationMin: req.DurationMinDefault,
                price: req.HourlyRate
            );
            offering.UpdateCurrency("TRY");

            _db.Offerings.Add(offering);
        }
        else
        {
            offering.UpdatePrice(req.HourlyRate);
            offering.UpdateDuration(req.DurationMinDefault > 0 ? req.DurationMinDefault : 60);
            offering.UpdateCurrency("TRY");
            offering.Activate();
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, offeringId = offering.Id });
    }
    // ---- Availability Template (Haftalık Şablon)

    /// <summary>
    /// Mentor: haftalık müsaitlik şablonunu getirir.
    /// </summary>
    [HttpGet("me/availability/template")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetMyAvailabilityTemplate(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAvailabilityTemplateQuery(), ct);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>
    /// Mentor: haftalık müsaitlik şablonunu kaydeder (upsert). Otomatik slot üretimi yapar.
    /// </summary>
    [HttpPut("me/availability/template")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> SaveMyAvailabilityTemplate(
        [FromBody] SaveAvailabilityTemplateCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { templateId = result.Data });
    }

    /// <summary>
    /// Mentor: tarih bazlı override ekler (tatil günü veya özel saat).
    /// </summary>
    [HttpPost("me/availability/override")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> AddAvailabilityOverride(
        [FromBody] AddAvailabilityOverrideCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { overrideId = result.Data });
    }

    /// <summary>
    /// Mentor: tarih bazlı override siler.
    /// </summary>
    [HttpDelete("me/availability/override/{overrideId}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> DeleteAvailabilityOverride(Guid overrideId, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var mentorUserId = _currentUser.UserId.Value;

        var template = await _db.AvailabilityTemplates
            .Include(t => t.Overrides)
            .FirstOrDefaultAsync(t => t.MentorUserId == mentorUserId && t.IsDefault, ct);

        if (template == null) return NotFound(new { errors = new[] { "Template not found" } });

        // Override'ı DB'den doğrudan bul ve sil (navigation property tracking sorunlarını önlemek için)
        var @override = await _db.AvailabilityOverrides
            .FirstOrDefaultAsync(o => o.Id == overrideId && o.TemplateId == template.Id, ct);
        if (@override == null) return NotFound(new { errors = new[] { "Override not found" } });

        _db.AvailabilityOverrides.Remove(@override);
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    // ---- Availability Slots


    /// <summary>
    /// Public: mentor'un uygun slotlarını getirir.
    /// Frontend: GET /mentors/{id}/availability?from=...&to=...
    /// </summary>
    [HttpGet("{id}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMentorAvailability(
        Guid id,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? offeringId,
        CancellationToken ct)

    {
        var result = await _mediator.Send(new GetMentorAvailabilityQuery(
            MentorUserId: id,
            From: from?.UtcDateTime,
            To: to?.UtcDateTime,
            IncludeBooked: false,
            OfferingId: offeringId), ct);


        if (!result.IsSuccess)

        {
            if (result.Errors.Any(e => e.ToLower().Contains("not found")))

                return NotFound(new { errors = result.Errors });


            return BadRequest(new { errors = result.Errors });
        }


        return Ok(result.Data);
    }


    /// <summary>
    /// Public: mentor'un belirli bir gün ve offering için uygun saat dilimlerini hesaplar.
    /// Buffer süresi ve mevcut booking'ler dikkate alınır.
    /// </summary>
    [HttpGet("{id}/available-time-slots")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableTimeSlots(
        Guid id,
        [FromQuery] Guid offeringId,
        [FromQuery] DateTime date,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAvailableTimeSlotsQuery(
            MentorUserId: id,
            OfferingId: offeringId,
            Date: date), ct);

        if (!result.IsSuccess)
        {
            if (result.Errors.Any(e => e.ToLower().Contains("not found")))
                return NotFound(new { errors = result.Errors });
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Mentor: kendi uygunluklarını getirir.
    /// Frontend: GET /mentors/me/availability?from=...&to=...
    /// </summary>
    [HttpGet("me/availability")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> GetMyAvailability(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)

    {
        var result = await _mediator.Send(new GetMyAvailabilityQuery(
            From: from?.UtcDateTime,
            To: to?.UtcDateTime), ct);


        if (!result.IsSuccess)

            return BadRequest(new { errors = result.Errors });


        return Ok(result.Data);
    }


    public sealed class CreateAvailabilitySlotRequest

    {
        public DateTimeOffset StartAt { get; set; }

        public DateTimeOffset EndAt { get; set; }


        // Frontend request'inde var (şimdilik opsiyonel). Model binder görüp ignore etmesin diye tuttuk.

        public object? Recurrence { get; set; }
    }


    /// <summary>
    /// Mentor: slot ekler.
    /// Frontend: POST /mentors/me/availability
    /// </summary>
    [HttpPost("me/availability")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> AddMyAvailabilitySlot(
        [FromBody] CreateAvailabilitySlotRequest req,
        CancellationToken ct)

    {
        var result = await _mediator.Send(new AddAvailabilitySlotCommand(
            StartAt: req.StartAt.UtcDateTime,
            EndAt: req.EndAt.UtcDateTime), ct);


        if (!result.IsSuccess)

            return BadRequest(new { errors = result.Errors });


        return Ok(new { id = result.Data });
    }


    /// <summary>
    /// Mentor: (booked olmayan) slot siler.
    /// Frontend: DELETE /mentors/me/availability/{slotId}
    /// </summary>
    [HttpDelete("me/availability/{slotId}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> DeleteMyAvailabilitySlot(Guid slotId, CancellationToken ct)

    {
        var result = await _mediator.Send(new DeleteAvailabilitySlotCommand(slotId), ct);

        if (!result.IsSuccess)

        {
            if (result.Errors.Any(e => e.ToLower().Contains("forbidden")))

                return Forbid();


            if (result.Errors.Any(e => e.ToLower().Contains("not found")))

                return NotFound(new { errors = result.Errors });


            return BadRequest(new { errors = result.Errors });
        }


        return Ok(new { ok = true });
    }
    // ---- ME publish (onboarding step 3)

    [HttpPost("me/publish")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> PublishMe(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return Unauthorized();
        var id = _currentUser.UserId.Value;

        var profile = await _db.MentorProfiles.FirstOrDefaultAsync(x => x.UserId == id, ct);
        if (profile == null) return BadRequest(new { errors = new[] { "Mentor profile not found" } });

        profile.Publish();
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }
}