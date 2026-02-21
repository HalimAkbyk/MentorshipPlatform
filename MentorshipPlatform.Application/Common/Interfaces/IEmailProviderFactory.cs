namespace MentorshipPlatform.Application.Common.Interfaces;

/// <summary>
/// Factory that returns the active email provider (SMTP or Resend) based on PlatformSettings.
/// </summary>
public interface IEmailProviderFactory
{
    Task<IEmailProvider> GetProviderAsync(CancellationToken ct = default);
}
