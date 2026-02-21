using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IEmailProviderFactory _providerFactory;
    private readonly ILogger<EmailService> _logger;
    private readonly IApplicationDbContext _context;

    public EmailService(
        IEmailProviderFactory providerFactory,
        ILogger<EmailService> logger,
        IApplicationDbContext context)
    {
        _providerFactory = providerFactory;
        _logger = logger;
        _context = context;
    }

    public async Task SendTemplatedEmailAsync(
        string templateKey,
        string to,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        try
        {
            // Look up the template from the database
            var template = await _context.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Key == templateKey && t.IsActive, ct);

            if (template == null)
            {
                _logger.LogWarning("Email template not found or inactive: {TemplateKey}. Skipping email to {To}.", templateKey, to);
                return;
            }

            // Resolve variables in subject and body
            var subject = ResolveVariables(template.Subject, variables);
            var body = ResolveVariables(template.Body, variables);

            var provider = await _providerFactory.GetProviderAsync(ct);
            await provider.SendAsync(to, subject, body, ct);

            _logger.LogInformation("Templated email [{TemplateKey}] sent to {To}", templateKey, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send templated email [{TemplateKey}] to {To}", templateKey, to);
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
