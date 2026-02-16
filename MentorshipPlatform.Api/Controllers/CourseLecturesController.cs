using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Courses.Commands.CreateLecture;
using MentorshipPlatform.Application.Courses.Commands.UpdateLecture;
using MentorshipPlatform.Application.Courses.Commands.DeleteLecture;
using MentorshipPlatform.Application.Courses.Commands.ReorderLectures;
using MentorshipPlatform.Application.Courses.Commands.GetVideoUploadUrl;
using MentorshipPlatform.Application.Courses.Commands.ConfirmVideoUpload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Authorize(Roles = "Mentor")]
public class CourseLecturesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStorageService _storageService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CourseLecturesController(
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
    /// <summary>Video dosyasını backend üzerinden yükle (CORS-safe proxy)</summary>
    [HttpPost("api/lectures/{lectureId:guid}/upload-video")]
    [RequestSizeLimit(524_288_000)] // 500 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    public async Task<IActionResult> UploadVideo(Guid lectureId, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Video dosyası gerekli" } });

        var userId = _currentUser.UserId;
        if (userId == null || userId == Guid.Empty)
            return Unauthorized();

        // Verify lecture exists and belongs to the mentor's course
        var lecture = await _context.CourseLectures
            .Include(l => l.Section)
                .ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == lectureId, ct);

        if (lecture == null)
            return NotFound(new { errors = new[] { "Ders bulunamadı" } });

        if (lecture.Section.Course.MentorUserId != userId)
            return Forbid();

        var courseId = lecture.Section.Course.Id;
        var sanitizedFileName = SanitizeFileName(file.FileName);
        var fileKey = $"courses/{courseId}/lectures/{lectureId}/{Guid.NewGuid()}_{sanitizedFileName}";

        using var stream = file.OpenReadStream();
        var uploadResult = await _storageService.UploadFileAsync(
            stream, sanitizedFileName, file.ContentType, userId.ToString()!, "course-video", ct);

        if (!uploadResult.Success)
            return BadRequest(new { errors = new[] { uploadResult.ErrorMessage ?? "Video yükleme başarısız" } });

        // Return the file key — frontend will call confirm-upload with duration
        return Ok(new { videoKey = uploadResult.FileKey });
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "video.mp4";
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
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "video";
        return sanitized + ext;
    }
}

// Request DTOs
public record CreateLectureRequest(string Title, string? Type, bool IsPreview, string? Description);
public record UpdateLectureRequest(string Title, string? Description, bool IsPreview, string? TextContent);
public record ReorderLecturesRequest(List<Guid> LectureIds);
public record GetUploadUrlRequest(string FileName, string ContentType);
public record ConfirmUploadRequest(string VideoKey, int DurationSec);
