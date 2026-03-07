using MediatR;
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

    public LibraryController(IMediator mediator)
    {
        _mediator = mediator;
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
