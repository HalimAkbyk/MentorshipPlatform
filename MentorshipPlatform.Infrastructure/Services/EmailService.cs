using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IEmailProviderFactory _providerFactory;
    private readonly ILogger<EmailService> _logger;
    private readonly IApplicationDbContext _context;
    private readonly IPlatformSettingService _settings;

    public EmailService(
        IEmailProviderFactory providerFactory,
        ILogger<EmailService> logger,
        IApplicationDbContext context,
        IPlatformSettingService settings)
    {
        _providerFactory = providerFactory;
        _logger = logger;
        _context = context;
        _settings = settings;
    }

    public async Task SendTemplatedEmailAsync(
        string templateKey,
        string to,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("📧 SendTemplatedEmailAsync called: templateKey={TemplateKey}, to={To}", templateKey, to);

            // Look up the template from the database
            var template = await _context.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Key == templateKey && t.IsActive, ct);

            if (template == null)
            {
                // Extra debug: check if template exists but is inactive
                var inactiveTemplate = await _context.NotificationTemplates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Key == templateKey, ct);

                if (inactiveTemplate != null)
                {
                    _logger.LogWarning("📧 Email template '{TemplateKey}' exists but is INACTIVE (IsActive=false). Skipping email to {To}.", templateKey, to);
                }
                else
                {
                    _logger.LogWarning("📧 Email template '{TemplateKey}' NOT FOUND in database. Total templates: {Count}. Skipping email to {To}.",
                        templateKey, await _context.NotificationTemplates.CountAsync(ct), to);
                }
                return;
            }

            _logger.LogInformation("📧 Template '{TemplateKey}' found (IsActive={IsActive}). Injecting global variables...", templateKey, template.IsActive);

            // Inject global layout variables (platformName, platformUrl, year)
            // These are used by WrapInLayout in every template
            await InjectGlobalVariables(variables, ct);

            // Resolve variables in subject and body
            var subject = ResolveVariables(template.Subject, variables);
            var body = ResolveVariables(template.Body, variables);

            var provider = await _providerFactory.GetProviderAsync(ct);
            _logger.LogInformation("📧 Sending email via provider {ProviderType}...", provider.GetType().Name);
            await provider.SendAsync(to, subject, body, ct);

            _logger.LogInformation("📧 Templated email [{TemplateKey}] successfully sent to {To}", templateKey, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "📧 Failed to send templated email [{TemplateKey}] to {To}", templateKey, to);
            // Don't throw - email failures shouldn't break the flow
        }
    }

    public async Task SendRawEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        try
        {
            var provider = await _providerFactory.GetProviderAsync(ct);
            await provider.SendAsync(to, subject, htmlBody, ct);

            _logger.LogInformation("Raw email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send raw email to {To}", to);
            // Don't throw - email failures shouldn't break the flow
        }
    }

    /// <summary>
    /// Injects global variables that every email template needs:
    /// platformName, platformUrl, year, supportEmail.
    /// Only adds if not already present in the variables dictionary.
    /// </summary>
    private async Task InjectGlobalVariables(Dictionary<string, string> variables, CancellationToken ct)
    {
        if (!variables.ContainsKey("platformName"))
        {
            variables["platformName"] = await _settings.GetAsync(
                PlatformSettings.PlatformName, "MentorHub", ct);
        }

        if (!variables.ContainsKey("platformUrl"))
        {
            variables["platformUrl"] = await _settings.GetAsync(
                PlatformSettings.FrontendUrl, "https://mentorship-platform.vercel.app", ct);
        }

        if (!variables.ContainsKey("year"))
        {
            variables["year"] = DateTime.UtcNow.Year.ToString();
        }

        if (!variables.ContainsKey("supportEmail"))
        {
            variables["supportEmail"] = await _settings.GetAsync(
                PlatformSettings.SupportEmail, "destek@mentorhub.com", ct);
        }
    }

    private static string ResolveVariables(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty);
        }
        return result;
    }
}
