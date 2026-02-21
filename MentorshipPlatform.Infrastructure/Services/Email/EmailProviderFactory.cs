using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services.Email;

public class EmailProviderFactory : IEmailProviderFactory
{
    private readonly IPlatformSettingService _settings;
    private readonly ILogger<EmailProviderFactory> _logger;

    public EmailProviderFactory(
        IPlatformSettingService settings,
        ILogger<EmailProviderFactory> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<IEmailProvider> GetProviderAsync(CancellationToken ct = default)
    {
        var provider = await _settings.GetAsync(PlatformSettings.EmailProvider, "smtp", ct);

        return provider.ToLowerInvariant() switch
        {
            "resend" => new ResendEmailProvider(_settings, _logger),
            _ => new SmtpEmailProvider(_settings, _logger),
        };
    }
}
