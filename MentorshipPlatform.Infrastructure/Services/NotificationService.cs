using MentorshipPlatform.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IEmailService emailService, ILogger<NotificationService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailService.SendRawEmailAsync(to, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email to {Email}", to);
        }
    }

    public Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SMS sending not yet implemented. Phone: {Phone}, Message: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }

    public Task SendPushNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Push notification not yet implemented. UserId: {UserId}, Title: {Title}", userId, title);
        return Task.CompletedTask;
    }
}
