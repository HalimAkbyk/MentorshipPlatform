using System.Collections.Concurrent;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Video.Commands.CreateVideoSession;
using MentorshipPlatform.Application.Video.Commands.GenerateVideoToken;
using MentorshipPlatform.Application.Video.Commands.EndVideoSession;
using MentorshipPlatform.Application.Video.Commands.LeaveRoom;
using MentorshipPlatform.Application.Video.Commands.HandleVideoWebhook;
using MentorshipPlatform.Application.Video.Queries.GetRoomStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api/video")]
[Authorize]
public class VideoController : ControllerBase
{
    // Cache whiteboard roomName → roomUuid so all participants join the same room
    private static readonly ConcurrentDictionary<string, string> _whiteboardRoomCache = new();

    private readonly IMediator _mediator;
    private readonly IVideoService _videoService;
    private readonly IFeatureFlagService _featureFlags;
    private readonly IAgoraWhiteboardService _whiteboardService;

    public VideoController(
        IMediator mediator,
        IVideoService videoService,
        IFeatureFlagService featureFlags,
        IAgoraWhiteboardService whiteboardService)
    {
        _mediator = mediator;
        _videoService = videoService;
        _featureFlags = featureFlags;
        _whiteboardService = whiteboardService;
    }

    [HttpPost("session")]
    public async Task<IActionResult> CreateSession([FromBody] CreateVideoSessionCommand command)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlags.VideoEnabled))
            return BadRequest(new { errors = new[] { "Video gorusme ozelligi gecici olarak devre disi birakilmistir." } });

        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(VideoTokenDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateToken([FromBody] GenerateVideoTokenCommand command)
    {
        if (!await _featureFlags.IsEnabledAsync(FeatureFlags.VideoEnabled))
            return BadRequest(new { errors = new[] { "Video gorusme ozelligi gecici olarak devre disi birakilmistir." } });

        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }
    [HttpGet("room/{roomName}/status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoomStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoomStatus(string roomName)
    {
        var result = await _mediator.Send(new GetRoomStatusQuery(roomName));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }
    [HttpPost("room/{roomName}/leave")]
    [ProducesResponseType(typeof(LeaveRoomResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> LeaveRoom(string roomName)
    {
        var result = await _mediator.Send(new LeaveRoomCommand(roomName));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpPost("room/{roomName}/end")]
    public async Task<IActionResult> EndSession(string roomName)
    {
        var result = await _mediator.Send(new EndVideoSessionCommand(roomName));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { message = "Session ended" });
    }

    [HttpPost("room/{roomName}/kick/{identity}")]
    public async Task<IActionResult> KickParticipant(string roomName, string identity)
    {
        var result = await _videoService.RemoveParticipantAsync(roomName, identity);
        if (!result)
            return BadRequest(new { error = "Failed to remove participant" });

        return Ok(new { message = "Participant removed" });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> TwilioWebhook([FromBody] TwilioWebhookDto webhook)
    {
        var result = await _mediator.Send(new HandleVideoWebhookCommand(
            RoomName: webhook.RoomName,
            ParticipantIdentity: webhook.ParticipantIdentity,
            EventType: webhook.StatusCallbackEvent
        ));

        // Twilio tekrar denemesin diye genelde 200 dönmek iyi
        return Ok();
    }

    /// <summary>Aktif video provider bilgisini don</summary>
    [HttpGet("provider")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProvider()
    {
        var provider = await _featureFlags.GetValueAsync(FeatureFlags.VideoProvider) ?? "twilio";
        var whiteboardEnabled = await _featureFlags.IsEnabledAsync("WHITEBOARD_ENABLED");
        return Ok(new { provider, whiteboardEnabled });
    }

    /// <summary>Whiteboard odasi olustur (veya mevcut olanı dön)</summary>
    [HttpPost("whiteboard/room")]
    public async Task<IActionResult> CreateWhiteboardRoom([FromBody] CreateWhiteboardRoomRequest request, CancellationToken ct)
    {
        var provider = await _featureFlags.GetValueAsync(FeatureFlags.VideoProvider) ?? "twilio";
        if (provider != "agora")
            return BadRequest(new { errors = new[] { "Whiteboard sadece Agora provider ile kullanilabilir." } });

        // Return cached room UUID if already created for this classroom
        if (_whiteboardRoomCache.TryGetValue(request.RoomName, out var cachedUuid))
            return Ok(new { roomUuid = cachedUuid });

        var result = await _whiteboardService.CreateRoomAsync(request.RoomName, ct);
        if (!result.Success)
            return BadRequest(new { errors = new[] { result.ErrorMessage ?? "Whiteboard odasi olusturulamadi." } });

        // Cache for future participants
        _whiteboardRoomCache[request.RoomName] = result.RoomUuid!;
        return Ok(new { roomUuid = result.RoomUuid });
    }

    /// <summary>Whiteboard token uret</summary>
    [HttpPost("whiteboard/token")]
    public async Task<IActionResult> GetWhiteboardToken([FromBody] WhiteboardTokenRequest request, CancellationToken ct)
    {
        var provider = await _featureFlags.GetValueAsync(FeatureFlags.VideoProvider) ?? "twilio";
        if (provider != "agora")
            return BadRequest(new { errors = new[] { "Whiteboard sadece Agora provider ile kullanilabilir." } });

        var token = await _whiteboardService.GenerateRoomTokenAsync(
            request.RoomUuid, request.UserId, request.IsWriter, ct);

        return Ok(new { token });
    }
}

public record CreateWhiteboardRoomRequest(string RoomName);
public record WhiteboardTokenRequest(string RoomUuid, string UserId, bool IsWriter = true);
