using System.Net;
using System.Net.Mail;
using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services.Email;

public class SmtpEmailProvider : IEmailProvider
{
    private readonly IPlatformSettingService _settings;
    private readonly ILogger _logger;

    public SmtpEmailProvider(IPlatformSettingService settings, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = await _settings.GetAsync(PlatformSettings.SmtpHost, "smtp.gmail.com", ct);
        var port = await _settings.GetIntAsync(PlatformSettings.SmtpPort, 587, ct);
        var username = await _settings.GetAsync(PlatformSettings.SmtpUsername, "", ct);
        var password = await _settings.GetAsync(PlatformSettings.SmtpPassword, "", ct);
        var fromEmail = await _settings.GetAsync(PlatformSettings.SmtpFromEmail, "noreply@mentorship-platform.com", ct);
        var fromName = await _settings.GetAsync(PlatformSettings.SmtpFromName, "MentorHub", ct);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogError("📧 [SMTP] CRITICAL: SMTP credentials not configured (username or password empty). Cannot send email to {To}: {Subject}", to, subject);
            throw new InvalidOperationException($"SMTP credentials not configured. Cannot send email to {to}");
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogError("📧 [SMTP] CRITICAL: SMTP host not configured. Cannot send email to {To}", to);
            throw new InvalidOperationException($"SMTP host not configured. Cannot send email to {to}");
        }

        _logger.LogInformation("📧 [SMTP] Sending to {To} via {Host}:{Port} from {From}...", to, host, port, fromEmail);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true
        };

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("📧 [SMTP] ✅ Email sent successfully to {To}: {Subject}", to, subject);
    }
}
