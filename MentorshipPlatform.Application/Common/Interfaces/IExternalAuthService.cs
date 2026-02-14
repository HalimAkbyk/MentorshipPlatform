namespace MentorshipPlatform.Application.Common.Interfaces;

public record ExternalUserInfo(
    string ExternalId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string? ProviderAccessToken = null);

public interface IExternalAuthService
{
    Task<ExternalUserInfo?> ValidateTokenAsync(string provider, string token);
    Task<ExternalUserInfo?> ExchangeCodeAsync(string provider, string code, string redirectUri);
}
