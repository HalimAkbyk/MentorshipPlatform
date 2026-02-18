using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Video.Commands.CreateVideoSession;
using MentorshipPlatform.Application.Video.Commands.GenerateVideoToken;
using MentorshipPlatform.Application.Video.Commands.EndVideoSession;
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
    private readonly IMediator _mediator;
    private readonly IVideoService _videoService;
    private readonly IFeatureFlagService _featureFlags;

    public VideoController(IMediator mediator, IVideoService videoService, IFeatureFlagService featureFlags)
    {
        _mediator = mediator;
        _videoService = videoService;
        _featureFlags = featureFlags;
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

}
