using System.Text.RegularExpressions;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Library.Commands.CreateLibraryItem;
using MentorshipPlatform.Application.Library.Commands.UpdateLibraryItem;
using MentorshipPlatform.Application.Library.Commands.DeleteLibraryItem;
using MentorshipPlatform.Application.Library.Queries.GetMyLibraryItems;
using MentorshipPlatform.Application.Library.Queries.GetLibraryItemById;
using MentorshipPlatform.Application.Library.Queries.GetLibraryStats;
using MentorshipPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/library")]
public class LibraryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStorageService _storageService;
    private readonly ICurrentUserService _currentUser;

    public LibraryController(IMediator mediator, IStorageService storageService, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _storageService = storageService;
        _currentUser = currentUser;
    }

    /// <summary>Yeni materyal ekle</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateLibraryItemCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { id = result.Data });
    }

    /// <summary>Materyallerimi listele</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyItems(
        [FromQuery] LibraryItemType? itemType,
        [FromQuery] FileFormat? fileFormat,
        [FromQuery] string? category,
        [FromQuery] string? subject,
        [FromQuery] string? search,
        [FromQuery] bool? isTemplate,
        [FromQuery] LibraryItemStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMyLibraryItemsQuery(
            itemType, fileFormat, category, subject, search, isTemplate, status, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Materyal detayi</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLibraryItemByIdQuery(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        if (result.Data == null) return NotFound();
        return Ok(result.Data);
    }

    /// <summary>Materyal guncelle</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLibraryItemRequest body, CancellationToken ct)
    {
        var command = new UpdateLibraryItemCommand(
            id,
            body.Title,
            body.Description,
            body.Category,
            body.Subject,
            body.TagsJson,
            body.IsTemplate,
            body.TemplateType,
            body.IsSharedWithStudents,
            body.ExternalUrl);

        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Materyal sil (soft delete)</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteLibraryItemCommand(id), ct);
        return result.IsSuccess ? Ok(new { ok = true }) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Kutuphane istatistikleri</summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLibraryStatsQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Dosya yukle (materyal icin)</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(524_288_000)] // 500 MB
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Dosya secilmedi" } });

        var userId = _currentUser.UserId;
        if (userId == null)
            return Unauthorized();

        var sanitizedFileName = SanitizeFileName(file.FileName);

        using var stream = file.OpenReadStream();
        var uploadResult = await _storageService.UploadFileAsync(
            stream, sanitizedFileName, file.ContentType, userId.ToString()!, "library", ct);

        if (!uploadResult.Success)
            return BadRequest(new { errors = new[] { uploadResult.ErrorMessage ?? "Dosya yukleme basarisiz" } });

        return Ok(new
        {
            fileUrl = uploadResult.PublicUrl,
            originalFileName = file.FileName,
            fileSizeBytes = file.Length
        });
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "file.bin";
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
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "file";
        return sanitized + ext;
    }
}

public record UpdateLibraryItemRequest(
    string Title,
    string? Description,
    string? Category,
    string? Subject,
    string? TagsJson,
    bool IsTemplate,
    string? TemplateType,
    bool IsSharedWithStudents,
    string? ExternalUrl);
