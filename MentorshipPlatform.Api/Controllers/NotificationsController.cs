using MediatR;
using MentorshipPlatform.Application.Notifications.Commands.MarkAllNotificationsRead;
using MentorshipPlatform.Application.Notifications.Commands.MarkNotificationRead;
using MentorshipPlatform.Application.Notifications.Queries.GetMyNotificationCount;
using MentorshipPlatform.Application.Notifications.Queries.GetMyNotifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Kullanıcının bildirimlerini getir</summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMyNotificationsQuery(page, pageSize), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { items = result.Data });
    }

    /// <summary>Okunmamış bildirim sayısı</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyNotificationCountQuery(), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(new { count = result.Data });
    }

    /// <summary>Tek bildirimi okundu işaretle</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new MarkNotificationReadCommand(id), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok();
    }

    /// <summary>Tüm bildirimleri okundu işaretle</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var result = await _mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok();
    }
}
