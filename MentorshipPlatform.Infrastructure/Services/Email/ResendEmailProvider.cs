using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services.Email;

public class ResendEmailProvider : IEmailProvider
{
    private readonly IPlatformSettingService _settings;
    private readonly ILogger _logger;

    public ResendEmailProvider(IPlatformSettingService settings, ILogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var apiKey = await _settings.GetAsync(PlatformSettings.ResendApiKey, "", ct);
        var fromEmail = await _settings.GetAsync(PlatformSettings.ResendFromEmail, "", ct);
        var fromName = await _settings.GetAsync(PlatformSettings.ResendFromName, "MentorHub", ct);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Resend API key not configured. Skipping email to {To}", to);
            return;
        }

        var from = string.IsNullOrWhiteSpace(fromName)
            ? fromEmail
            : $"{fromName} <{fromEmail}>";

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            from,
            to = new[] { to },
            subject,
            html = htmlBody
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("https://api.resend.com/emails", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("[Resend] Email sent to {To}: {Subject}", to, subject);
        }
        else
        {
            _logger.LogError("[Resend] Failed to send email to {To}: {StatusCode} - {Response}",
                to, response.StatusCode, responseBody);
        }
    }
}
