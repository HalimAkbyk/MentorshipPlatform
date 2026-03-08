namespace MentorshipPlatform.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AgoraWhiteboardService : IAgoraWhiteboardService
{
    private readonly AgoraOptions _options;
    private readonly ILogger<AgoraWhiteboardService> _logger;
    private readonly HttpClient _httpClient;

    public AgoraWhiteboardService(
        IOptions<AgoraOptions> options,
        ILogger<AgoraWhiteboardService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AgoraWhiteboard");

        // Whiteboard API uses token auth, not basic auth
        _httpClient.BaseAddress = new Uri("https://api.netless.link/v5/");
        _httpClient.DefaultRequestHeaders.Add("region", _options.Whiteboard.Region);
    }

    public async Task<WhiteboardRoomResult> CreateRoomAsync(string roomName, CancellationToken ct = default)
    {
        try
        {
            // Generate SDK token for API calls
            var sdkToken = GenerateSdkToken(0); // admin role
            _httpClient.DefaultRequestHeaders.Remove("token");
            _httpClient.DefaultRequestHeaders.Add("token", sdkToken);

            var payload = new { name = roomName, isRecord = false };
            var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("rooms", content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var uuid = doc.RootElement.GetProperty("uuid").GetString()!;
                _logger.LogInformation("Created whiteboard room: {RoomName} -> {Uuid}", roomName, uuid);
                return new WhiteboardRoomResult(true, uuid, null);
            }

            _logger.LogWarning("Failed to create whiteboard room: {Status} {Body}", response.StatusCode, json);
            return new WhiteboardRoomResult(false, null, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating whiteboard room {RoomName}", roomName);
            return new WhiteboardRoomResult(false, null, ex.Message);
        }
    }

    public async Task<string> GenerateRoomTokenAsync(
        string roomUuid, string userId, bool isWriter, CancellationToken ct = default)
    {
        // Generate room token locally using HMAC
        var role = isWriter ? 1 : 2; // 1=writer, 2=reader
        return GenerateRoomToken(roomUuid, role);
    }

    private string GenerateSdkToken(int role)
    {
        // Netless SDK token format
        var ak = _options.Whiteboard.AccessKey;
        var sk = _options.Whiteboard.SecretKey;

        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payload = JsonSerializer.Serialize(new
        {
            ak,
            nonce = Guid.NewGuid().ToString("N"),
            role,
            iat = now,
            exp = now + 3600000 // 1 hour
        });
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var toSign = $"{header}.{payloadB64}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sk));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"NETLESSSDK_{header}.{payloadB64}.{sig}";
    }

    private string GenerateRoomToken(string roomUuid, int role)
    {
        var ak = _options.Whiteboard.AccessKey;
        var sk = _options.Whiteboard.SecretKey;

        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payload = JsonSerializer.Serialize(new
        {
            ak,
            nonce = Guid.NewGuid().ToString("N"),
            role,
            iat = now,
            exp = now + 14400000, // 4 hours
            uuid = roomUuid
        });
        var payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var toSign = $"{header}.{payloadB64}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sk));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"NETLESSSDK_{header}.{payloadB64}.{sig}";
    }
}
