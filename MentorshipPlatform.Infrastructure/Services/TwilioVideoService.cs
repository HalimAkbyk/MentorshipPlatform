using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Video.V1;
using Twilio.Rest.Video.V1.Room;

namespace MentorshipPlatform.Infrastructure.Services;

public class TwilioVideoService : IVideoService
{
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioVideoService> _logger;

    public TwilioVideoService(
        IOptions<TwilioOptions> options,
        ILogger<TwilioVideoService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "TwilioVideoService init — AccountSid={Sid}, ApiKeySid={Key}, StatusCallbackUrl={Url}",
            string.IsNullOrEmpty(_options.AccountSid) ? "(EMPTY)" : _options.AccountSid[..Math.Min(8, _options.AccountSid.Length)] + "***",
            string.IsNullOrEmpty(_options.ApiKeySid) ? "(EMPTY)" : _options.ApiKeySid[..Math.Min(8, _options.ApiKeySid.Length)] + "***",
            _options.StatusCallbackUrl ?? "(null)");

        if (string.IsNullOrEmpty(_options.AccountSid) || string.IsNullOrEmpty(_options.AuthToken))
        {
            _logger.LogError("Twilio AccountSid or AuthToken is empty! Video features will not work.");
            return;
        }

        TwilioClient.Init(_options.AccountSid, _options.AuthToken);
    }

    public async Task<VideoRoomResult> CreateRoomAsync(
        string resourceType,
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use kebab-case format to match frontend convention (e.g., "group-class-{id}")
            var roomName = resourceType switch
            {
                "GroupClass" => $"group-class-{resourceId}",
                _ => $"{resourceType}-{resourceId}"
            };
            
            var room = await RoomResource.CreateAsync(
                uniqueName: roomName,
                type: RoomResource.RoomTypeEnum.Group,
                maxParticipants: 50,
                statusCallback: new Uri(_options.StatusCallbackUrl)
            );

            return new VideoRoomResult(true, room.UniqueName, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Twilio room");
            return new VideoRoomResult(false, string.Empty, ex.Message);
        }
    }

    public async Task<VideoTokenResult> GenerateTokenAsync(
        string roomName,
        Guid userId,
        string participantName,
        bool isHost = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If host, ensure the Twilio room exists as Group type
            if (isHost)
            {
                try
                {
                    await RoomResource.CreateAsync(
                        uniqueName: roomName,
                        type: RoomResource.RoomTypeEnum.Group,
                        maxParticipants: 10,
                        statusCallback: string.IsNullOrEmpty(_options.StatusCallbackUrl)
                            ? null
                            : new Uri(_options.StatusCallbackUrl)
                    );
                    _logger.LogInformation("Created Twilio Group room: {RoomName}", roomName);
                }
                catch (Exception ex) when (ex.Message.Contains("Room exists"))
                {
                    _logger.LogInformation("Twilio room already exists: {RoomName}", roomName);
                }
            }

            var identity = $"{userId}|{participantName}";

            var grant = new VideoGrant
            {
                Room = roomName
            };

            var grants = new HashSet<IGrant> { grant };

            var token = new Token(
                _options.AccountSid,
                _options.ApiKeySid,
                _options.ApiKeySecret,
                identity,
                grants: grants,
                expiration: DateTime.UtcNow.AddHours(4)
            );

            return new VideoTokenResult(true, token.ToJwt(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Twilio token for room {RoomName}. ExType={ExType}, Message={Msg}",
                roomName, ex.GetType().FullName, ex.Message);
            return new VideoTokenResult(false, null, ex.Message);
        }
    }
    public async Task<bool> RemoveParticipantAsync(
        string roomName,
        string participantIdentity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fetch the Twilio room by name
            var rooms = await RoomResource.ReadAsync(uniqueName: roomName, limit: 1);
            var room = rooms.FirstOrDefault();
            if (room == null)
            {
                _logger.LogWarning("Room not found for kick: {RoomName}", roomName);
                return false;
            }

            // Find the participant and disconnect them
            var participants = await ParticipantResource.ReadAsync(
                pathRoomSid: room.Sid,
                limit: 50
            );

            var participant = participants.FirstOrDefault(p => p.Identity == participantIdentity);
            if (participant == null)
            {
                _logger.LogWarning("Participant not found: {Identity} in room {RoomName}", participantIdentity, roomName);
                return false;
            }

            await ParticipantResource.UpdateAsync(
                pathRoomSid: room.Sid,
                pathSid: participant.Sid,
                status: ParticipantResource.StatusEnum.Disconnected
            );

            _logger.LogInformation("Kicked participant {Identity} from room {RoomName}", participantIdentity, roomName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing participant {Identity} from room {RoomName}", participantIdentity, roomName);
            return false;
        }
    }

    public async Task<(bool Exists, bool IsInProgress, int ParticipantCount)> GetRoomInfoAsync(
        string roomName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rooms = await RoomResource.ReadAsync(
                uniqueName: roomName,
                status: RoomResource.RoomStatusEnum.InProgress,
                limit: 1);
            var room = rooms.FirstOrDefault();
            if (room == null)
                return (false, false, 0);

            // Count connected participants
            var participants = await ParticipantResource.ReadAsync(
                pathRoomSid: room.Sid,
                status: ParticipantResource.StatusEnum.Connected,
                limit: 50);
            var count = participants.Count();

            return (true, true, count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Twilio room info for {RoomName}", roomName);
            return (false, false, 0);
        }
    }
}

public class TwilioOptions
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string ApiKeySid { get; set; } = string.Empty;
    public string ApiKeySecret { get; set; } = string.Empty;
    public string StatusCallbackUrl { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    
}