namespace MentorshipPlatform.Application.Common.Interfaces;

public interface ISmsService
{
    Task SendBookingReminderSmsAsync(string phoneNumber, string mentorName, DateTime startAt, CancellationToken cancellationToken = default);
    Task SendVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}