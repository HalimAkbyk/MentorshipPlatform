namespace MentorshipPlatform.Infrastructure.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AgoraVideoService : IVideoService
{
    private readonly AgoraOptions _options;
    private readonly ILogger<AgoraVideoService> _logger;
    private readonly HttpClient _httpClient;

    public AgoraVideoService(
        IOptions<AgoraOptions> options,
        ILogger<AgoraVideoService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Agora");

        // Set up Basic auth for REST API
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.CustomerId}:{_options.CustomerSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.BaseAddress = new Uri("https://api.agora.io/");
    }

    public async Task<VideoRoomResult> CreateRoomAsync(
        string resourceType, Guid resourceId, CancellationToken ct = default)
    {
        try
        {
            var roomName = resourceType switch
            {
                "GroupClass" => $"group-class-{resourceId}",
                _ => $"{resourceType}-{resourceId}"
            };

            // Agora channels are created implicitly when users join
            // No explicit room creation needed, but we return the channel name
            _logger.LogInformation("Agora channel prepared: {RoomName}", roomName);
            return new VideoRoomResult(true, roomName, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing Agora channel");
            return new VideoRoomResult(false, string.Empty, ex.Message);
        }
    }

    public async Task<VideoTokenResult> GenerateTokenAsync(
        string roomName, Guid userId, string participantName,
        bool isHost = false, CancellationToken ct = default)
    {
        try
        {
            var identity = $"{userId}|{participantName}";
            var expireTs = (uint)DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();

            var token = AgoraTokenBuilder.BuildToken(
                _options.AppId,
                _options.AppCertificate,
                roomName,
                identity,
                expireTs);

            _logger.LogInformation(
                "Generated Agora token for {Identity} in channel {Channel}",
                identity, roomName);

            return new VideoTokenResult(true, token, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Agora token for channel {Channel}", roomName);
            return new VideoTokenResult(false, null, ex.Message);
        }
    }

    public async Task<bool> RemoveParticipantAsync(
        string roomName, string participantIdentity, CancellationToken ct = default)
    {
        try
        {
            // Agora REST API: kick user from channel
            // POST /dev/v1/kicking-rule
            var payload = new
            {
                appid = _options.AppId,
                cname = roomName,
                uid = 0, // kick by privileges
                ip = "",
                time = 0, // permanent until rule deleted
                privileges = new[] { "join_channel" }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("dev/v1/kicking-rule", content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Kicked participant {Identity} from Agora channel {Channel}",
                    participantIdentity, roomName);
                return true;
            }

            _logger.LogWarning("Failed to kick participant: {Status}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error kicking participant from Agora channel {Channel}", roomName);
            return false;
        }
    }

    public async Task<bool> CompleteRoomAsync(string roomName, CancellationToken ct = default)
    {
        try
        {
            // Agora channels auto-close when all participants leave
            // We can force-close by adding a kicking rule for all users
            _logger.LogInformation("Agora channel {Channel} marked as completed", roomName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing Agora channel {Channel}", roomName);
            return false;
        }
    }

    public async Task<(bool Exists, bool IsInProgress, int ParticipantCount)> GetRoomInfoAsync(
        string roomName, CancellationToken ct = default)
    {
        try
        {
            // Agora REST API: query channel info
            var response = await _httpClient.GetAsync(
                $"dev/v1/channel/user/{_options.AppId}/{roomName}", ct);

            if (!response.IsSuccessStatusCode)
                return (false, false, 0);

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var data = root.GetProperty("data");
                var inChannel = data.TryGetProperty("channel_exist", out var exists) && exists.GetBoolean();
                var userCount = 0;

                if (data.TryGetProperty("users", out var users) && users.ValueKind == JsonValueKind.Array)
                    userCount = users.GetArrayLength();

                return (inChannel, inChannel, userCount);
            }

            return (false, false, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying Agora channel {Channel}", roomName);
            return (false, false, 0);
        }
    }
}
