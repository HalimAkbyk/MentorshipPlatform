using Hangfire;
using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Jobs;
using MentorshipPlatform.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.DomainEventHandlers;

public class BookingConfirmedEventHandler : INotificationHandler<BookingConfirmedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IEmailService _emailService;
    private readonly ILogger<BookingConfirmedEventHandler> _logger;

    public BookingConfirmedEventHandler(
        IApplicationDbContext context,
        IBackgroundJobClient backgroundJobs,
        IEmailService emailService,
        ILogger<BookingConfirmedEventHandler> logger)
    {
        _context = context;
        _backgroundJobs = backgroundJobs;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(BookingConfirmedEvent notification, CancellationToken cancellationToken)
    {
        // Get booking details
        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == notification.BookingId, cancellationToken);

        if (booking == null) return;

        var trCulture = new System.Globalization.CultureInfo("tr-TR");

        // Send confirmation email to student
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.BookingConfirmed,
                booking.Student.Email!,
                new Dictionary<string, string>
                {
                    ["mentorName"] = booking.Mentor.DisplayName,
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["bookingTime"] = booking.StartAt.ToString("HH:mm"),
                    ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking confirmation email to student for {BookingId}", notification.BookingId);
        }

        // Send confirmation email to mentor
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.BookingConfirmedMentor,
                booking.Mentor.Email!,
                new Dictionary<string, string>
                {
                    ["studentName"] = booking.Student.DisplayName,
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["bookingTime"] = booking.StartAt.ToString("HH:mm"),
                    ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking confirmation email to mentor for {BookingId}", notification.BookingId);
        }

        // Schedule reminder jobs
        var reminderTime24h = notification.StartAt.AddHours(-24);
        var reminderTime1h = notification.StartAt.AddHours(-1);
        var reminderTime10m = notification.StartAt.AddMinutes(-10);

        if (reminderTime24h > DateTime.UtcNow)
        {
            _backgroundJobs.Schedule<SendBookingReminderJob>(
                job => job.Execute(notification.BookingId, "24h"),
                reminderTime24h);
        }

        if (reminderTime1h > DateTime.UtcNow)
        {
            _backgroundJobs.Schedule<SendBookingReminderJob>(
                job => job.Execute(notification.BookingId, "1h"),
                reminderTime1h);
        }

        if (reminderTime10m > DateTime.UtcNow)
        {
            _backgroundJobs.Schedule<SendBookingReminderJob>(
                job => job.Execute(notification.BookingId, "10m"),
                reminderTime10m);
        }

        _logger.LogInformation(
            "Booking confirmed notification sent and reminders scheduled for {BookingId}", 
            notification.BookingId);
    }
}