namespace MentorshipPlatform.Application.Common.Interfaces;

/// <summary>
/// High-level email service. Sends templated emails (DB-based templates with variable substitution)
/// and raw emails (for bulk notifications and admin-composed content).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a templated email. Looks up the template by key from DB, resolves {{variables}},
    /// and sends via the active provider (SMTP or Resend).
    /// If the template is not found or inactive, the email is silently skipped.
    /// </summary>
    Task SendTemplatedEmailAsync(
        string templateKey,
        string to,
        Dictionary<string, string> variables,
        CancellationToken ct = default);

    /// <summary>
    /// Send a raw email with explicit subject and body. Used for bulk notifications
    /// and admin-composed emails that don't use a template.
    /// </summary>
    Task SendRawEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}
