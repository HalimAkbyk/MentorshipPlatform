using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Courses.Commands.CreateCourse;
using MentorshipPlatform.Application.Courses.Commands.UpdateCourse;
using MentorshipPlatform.Application.Courses.Commands.PublishCourse;
using MentorshipPlatform.Application.Courses.Commands.ResubmitForReview;
using MentorshipPlatform.Application.Courses.Commands.ArchiveCourse;
using MentorshipPlatform.Application.Courses.Commands.DeleteCourse;
using MentorshipPlatform.Application.Courses.Queries.GetMyCourses;
using MentorshipPlatform.Application.Courses.Queries.GetCourseForEdit;
using MentorshipPlatform.Application.Courses.Queries.GetPublicCourses;
using MentorshipPlatform.Application.Courses.Queries.GetCourseDetail;
using MentorshipPlatform.Application.Courses.Queries.GetPreviewLecture;
using MentorshipPlatform.Application.Courses.Queries.GetMyCourseReviewStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStorageService _storageService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CoursesController(
        IMediator mediator,
        IStorageService storageService,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _storageService = storageService;
        _context = context;
        _currentUser = currentUser;
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
            body.CoverImagePosition,
            body.CoverImageTransform,
            body.PromoVideoKey,
            body.WhatYouWillLearn,
            body.Requirements,
            body.TargetAudience);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kursu onaya gönder (yayınla → inceleme akışına gider)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishCourseRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishCourseCommand(id, body?.MentorNotes), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kurs inceleme durumunu getir (mentor)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpGet("{id:guid}/review-status")]
    public async Task<IActionResult> GetReviewStatus(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyCourseReviewStatusQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Kursu tekrar onaya gönder (revizyon sonrası)</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost("{id:guid}/resubmit")]
    public async Task<IActionResult> Resubmit(Guid id, [FromBody] ResubmitCourseRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ResubmitCourseForReviewCommand(id, body?.MentorNotes), ct);
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

    /// <summary>Ücretsiz önizleme dersi (herkese açık)</summary>
    [AllowAnonymous]
    [HttpGet("{courseId:guid}/preview/{lectureId:guid}")]
    public async Task<IActionResult> GetPreviewLecture(Guid courseId, Guid lectureId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPreviewLectureQuery(courseId, lectureId), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Kurs kapak görseli yükle</summary>
    [Authorize(Roles = "Mentor")]
    [HttpPost("{id:guid}/upload-cover")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadCoverImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Dosya seçilmedi" } });

        var userId = _currentUser.UserId;
        if (userId == null)
            return Unauthorized();

        var course = await _context.Courses
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (course == null)
            return NotFound(new { errors = new[] { "Kurs bulunamadı" } });

        if (course.MentorUserId != userId)
            return Forbid();

        var sanitizedFileName = SanitizeFileName(file.FileName);

        using var stream = file.OpenReadStream();
        var uploadResult = await _storageService.UploadFileAsync(
            stream, sanitizedFileName, file.ContentType, userId.ToString()!, "course-cover", ct);

        if (!uploadResult.Success)
            return BadRequest(new { errors = new[] { uploadResult.ErrorMessage ?? "Kapak görseli yükleme başarısız" } });

        course.Update(course.Title, course.ShortDescription, course.Description, course.Price,
            course.Category, course.Language, course.Level, uploadResult.PublicUrl,
            course.CoverImagePosition, course.CoverImageTransform, course.PromoVideoKey,
            course.WhatYouWillLearnJson, course.RequirementsJson, course.TargetAudienceJson);
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
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"-+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "image";
        return sanitized + ext;
    }
}

// Request DTOs
public record PublishCourseRequest(string? MentorNotes);

public record ResubmitCourseRequest(string? MentorNotes);

public record UpdateCourseRequest(
    string Title,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string? Category,
    string? Language,
    string? Level,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    string? PromoVideoKey,
    List<string>? WhatYouWillLearn,
    List<string>? Requirements,
    List<string>? TargetAudience);
