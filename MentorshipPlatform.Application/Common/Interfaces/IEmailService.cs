namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendBookingConfirmationAsync(string to, string mentorName, DateTime startAt, CancellationToken cancellationToken = default);
    Task SendBookingReminderAsync(string to, string mentorName, DateTime startAt, string timeframe, CancellationToken cancellationToken = default);
    Task SendBookingCancelledAsync(string to, string reason, CancellationToken cancellationToken = default);
    Task SendVerificationApprovedAsync(string to, string verificationType, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string to, string displayName, CancellationToken cancellationToken = default);
}