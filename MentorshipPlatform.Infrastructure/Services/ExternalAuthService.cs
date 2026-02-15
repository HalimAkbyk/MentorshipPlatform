using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Apis.Auth;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace MentorshipPlatform.Infrastructure.Services;

public class ExternalAuthService : IExternalAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalAuthService> _logger;

    public ExternalAuthService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalAuthService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ExternalUserInfo?> ValidateTokenAsync(string provider, string token)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => await ValidateGoogleTokenAsync(token),
            "microsoft" => await ValidateMicrosoftTokenAsync(token),
            "linkedin" => await ValidateLinkedInTokenAsync(token),
            "apple" => await ValidateAppleTokenAsync(token),
            _ => throw new ArgumentException($"Unsupported provider: {provider}")
        };
    }

    public async Task<ExternalUserInfo?> ExchangeCodeAsync(string provider, string code, string redirectUri)
    {
        return provider.ToLowerInvariant() switch
        {
            "linkedin" => await ExchangeLinkedInCodeAsync(code, redirectUri),
            _ => throw new ArgumentException($"Code exchange not supported for: {provider}")
        };
    }

    private async Task<ExternalUserInfo?> ValidateGoogleTokenAsync(string token)
    {
        // First try as access_token (from implicit flow / useGoogleLogin)
        // Access tokens start with "ya29." — use Google userinfo endpoint
        if (token.StartsWith("ya29."))
        {
            return await ValidateGoogleAccessTokenAsync(token);
        }

        // Otherwise treat as ID token (JWT) for auth-code flow
        try
        {
            var clientId = _configuration["ExternalAuth:Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);
            return new ExternalUserInfo(
                payload.Subject,
                payload.Email,
                payload.Name ?? payload.Email,
                payload.Picture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed");
            return null;
        }
    }

    private async Task<ExternalUserInfo?> ValidateGoogleAccessTokenAsync(string accessToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google userinfo request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            var sub = json.GetProperty("sub").GetString()!;
            var email = json.TryGetProperty("email", out var emailProp)
                ? emailProp.GetString() : null;
            var name = json.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() : null;
            var picture = json.TryGetProperty("picture", out var picProp)
                ? picProp.GetString() : null;

            if (email == null) return null;

            return new ExternalUserInfo(sub, email, name ?? email, picture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google access token validation failed");
            return null;
        }
    }

    private async Task<ExternalUserInfo?> ValidateMicrosoftTokenAsync(string accessToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var id = json.GetProperty("id").GetString()!;
            var email = json.TryGetProperty("mail", out var mailProp) && mailProp.ValueKind != JsonValueKind.Null
                ? mailProp.GetString()
                : json.TryGetProperty("userPrincipalName", out var upnProp)
                    ? upnProp.GetString()
                    : null;
            var displayName = json.TryGetProperty("displayName", out var dnProp)
                ? dnProp.GetString()
                : email;

            if (email == null) return null;

            return new ExternalUserInfo(id, email, displayName ?? email, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Microsoft token validation failed");
            return null;
        }
    }

    private async Task<ExternalUserInfo?> ExchangeLinkedInCodeAsync(string code, string redirectUri)
    {
        try
        {
            var clientId = _configuration["ExternalAuth:LinkedIn:ClientId"];
            var clientSecret = _configuration["ExternalAuth:LinkedIn:ClientSecret"];

            var client = _httpClientFactory.CreateClient();

            // Step 1: Exchange code for access token
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!)
            });

            var tokenResponse = await client.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", tokenRequest);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorBody = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("LinkedIn token exchange failed: {StatusCode} {Body}", tokenResponse.StatusCode, errorBody);
                return null;
            }

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = tokenJson.GetProperty("access_token").GetString()!;

            // Step 2: Use access token to get user info
            var userInfo = await ValidateLinkedInTokenAsync(accessToken);
            if (userInfo == null) return null;

            // Attach the provider access token so it can be reused for ROLE_REQUIRED retries
            return userInfo with { ProviderAccessToken = accessToken };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LinkedIn code exchange failed");
            return null;
        }
    }

    private async Task<ExternalUserInfo?> ValidateLinkedInTokenAsync(string accessToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync("https://api.linkedin.com/v2/userinfo");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var sub = json.GetProperty("sub").GetString()!;
            var email = json.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
            var name = json.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var picture = json.TryGetProperty("picture", out var picProp) ? picProp.GetString() : null;

            if (email == null) return null;

            return new ExternalUserInfo(sub, email, name ?? email, picture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LinkedIn token validation failed");
            return null;
        }
    }

    private async Task<ExternalUserInfo?> ValidateAppleTokenAsync(string idToken)
    {
        try
        {
            // Fetch Apple's public JWKS keys
            var client = _httpClientFactory.CreateClient();
            var jwksResponse = await client.GetAsync("https://appleid.apple.com/auth/keys");
            if (!jwksResponse.IsSuccessStatusCode) return null;
            var jwksJson = await jwksResponse.Content.ReadAsStringAsync();

            var jwks = new JsonWebKeySet(jwksJson);
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "https://appleid.apple.com",
                ValidateAudience = true,
                ValidAudience = _configuration["ExternalAuth:Apple:ClientId"],
                ValidateLifetime = true,
                IssuerSigningKeys = jwks.GetSigningKeys()
            };

            var principal = tokenHandler.ValidateToken(idToken, validationParameters, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;

            if (sub == null || email == null) return null;

            return new ExternalUserInfo(sub, email, email, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Apple token validation failed");
            return null;
        }
    }
}
