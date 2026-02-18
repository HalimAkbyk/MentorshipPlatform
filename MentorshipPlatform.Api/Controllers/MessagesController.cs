using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Messages.Commands.MarkMessagesAsRead;
using MentorshipPlatform.Application.Messages.Commands.ReportMessage;
using MentorshipPlatform.Application.Messages.Commands.SendMessage;
using MentorshipPlatform.Application.Messages.Queries.GetBookingMessages;
using MentorshipPlatform.Application.Messages.Queries.GetMyConversations;
using MentorshipPlatform.Application.Messages.Queries.GetUnreadMessageCount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagService _featureFlags;

    public MessagesController(IMediator mediator, IFeatureFlagService featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    /// <summary>Send a message in a booking conversation</summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageCommand command)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlags.ChatEnabled))
            return BadRequest(new { errors = new[] { "Mesajlasma ozelligi gecici olarak devre disi birakilmistir." } });

        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { messageId = result.Data });
    }

    /// <summary>Get messages for a booking</summary>
    [HttpGet("booking/{bookingId:guid}")]
    public async Task<IActionResult> GetBookingMessages(
        Guid bookingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetBookingMessagesQuery(bookingId, page, pageSize));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Mark all messages in a booking as read</summary>
    [HttpPost("booking/{bookingId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid bookingId)
    {
        var result = await _mediator.Send(new MarkMessagesAsReadCommand(bookingId));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok();
    }

    /// <summary>Get all conversations for current user</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var result = await _mediator.Send(new GetMyConversationsQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Get unread message count for current user</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await _mediator.Send(new GetUnreadMessageCountQuery());
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    /// <summary>Report a message</summary>
    [HttpPost("{messageId:guid}/report")]
    public async Task<IActionResult> ReportMessage(
        Guid messageId, [FromBody] ReportMessageRequest body)
    {
        var result = await _mediator.Send(new ReportMessageCommand(messageId, body.Reason));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { reportId = result.Data });
    }
}

public record ReportMessageRequest(string Reason);
