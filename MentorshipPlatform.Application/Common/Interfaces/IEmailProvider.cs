namespace MentorshipPlatform.Application.Common.Interfaces;

/// <summary>
/// Low-level email sending abstraction. Implementations: SmtpEmailProvider, ResendEmailProvider.
/// </summary>
public interface IEmailProvider
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
