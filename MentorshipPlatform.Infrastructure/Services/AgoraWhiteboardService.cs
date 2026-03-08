namespace MentorshipPlatform.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
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
            // Generate SDK token for API calls (admin role = 0)
            var sdkToken = CreateNetlessToken("NETLESSSDK", 0, 3600_000, null);
            _httpClient.DefaultRequestHeaders.Remove("token");
            _httpClient.DefaultRequestHeaders.Add("token", sdkToken);

            // Netless v5 API: POST /rooms accepts only specific fields (limit, isRecord)
            // "name" is not a valid field and causes "disable input" error
            var payload = new { isRecord = false };
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
        // 1=writer, 2=reader. 4 hours lifespan.
        var role = isWriter ? 1 : 2;
        return CreateNetlessToken("NETLESSROOM", role, 14400_000, roomUuid);
    }

    /// <summary>
    /// Generate a Netless token following the official algorithm from:
    /// https://github.com/netless-io/netless-token
    ///
    /// Steps:
    /// 1. Build sorted key-value map (all values as strings)
    /// 2. Serialize to JSON
    /// 3. HMAC-SHA256 sign the JSON, hex-encode the signature
    /// 4. Add sig to map, build URL-encoded query string
    /// 5. Base64url-encode the query string
    /// 6. Prepend prefix (NETLESSSDK_ or NETLESSROOM_)
    /// </summary>
    private string CreateNetlessToken(string prefix, int role, long lifespanMs, string? roomUuid)
    {
        var ak = _options.Whiteboard.AccessKey;
        var sk = _options.Whiteboard.SecretKey;

        // Step 1: Build the map (all values as strings, sorted by key)
        var map = new SortedDictionary<string, string>
        {
            ["ak"] = ak,
            ["nonce"] = Guid.NewGuid().ToString(),
            ["role"] = role.ToString(),
        };

        if (lifespanMs > 0)
        {
            var expireAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + lifespanMs;
            map["expireAt"] = expireAt.ToString();
        }

        if (!string.IsNullOrEmpty(roomUuid))
        {
            map["uuid"] = roomUuid;
        }

        // Step 2: Serialize sorted map to JSON
        var jsonContent = JsonSerializer.Serialize(map);

        // Step 3: HMAC-SHA256 sign, hex-encode
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sk));
        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonContent));
        var sigHex = Convert.ToHexString(sigBytes).ToLowerInvariant();

        // Step 4: Add sig to map, build query string
        map["sig"] = sigHex;

        var queryParts = new List<string>();
        foreach (var kvp in map)
        {
            queryParts.Add($"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}");
        }
        var queryString = string.Join("&", queryParts);

        // Step 5: Base64url-encode
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(queryString))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        // Step 6: Prepend prefix
        return $"{prefix}_{base64}";
    }
}
