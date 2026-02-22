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
        _logger.LogInformation("📧 BookingConfirmedEventHandler triggered for BookingId={BookingId}", notification.BookingId);

        // Get booking details
        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == notification.BookingId, cancellationToken);

        if (booking == null)
        {
            _logger.LogError("📧 BookingConfirmedEventHandler: Booking not found! BookingId={BookingId}", notification.BookingId);
            return;
        }

        _logger.LogInformation("📧 Booking found: Student={StudentEmail}, Mentor={MentorEmail}, Offering={Offering}",
            booking.Student?.Email ?? "NULL", booking.Mentor?.Email ?? "NULL", booking.Offering?.Title ?? "NULL");

        var trCulture = new System.Globalization.CultureInfo("tr-TR");

        // Send confirmation email to student
        try
        {
            if (string.IsNullOrWhiteSpace(booking.Student?.Email))
            {
                _logger.LogWarning("📧 Student email is null/empty for BookingId={BookingId}. Skipping student email.", notification.BookingId);
            }
            else
            {
                _logger.LogInformation("📧 Sending booking_confirmed to student: {Email}", booking.Student.Email);
                await _emailService.SendTemplatedEmailAsync(
                    EmailTemplateKeys.BookingConfirmed,
                    booking.Student.Email,
                    new Dictionary<string, string>
                    {
                        ["mentorName"] = booking.Mentor.DisplayName,
                        ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                        ["bookingTime"] = booking.StartAt.ToString("HH:mm"),
                        ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                    },
                    cancellationToken);
                _logger.LogInformation("📧 ✅ booking_confirmed email sent to student {Email}", booking.Student.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "📧 ❌ Failed to send booking_confirmed email to student for BookingId={BookingId}", notification.BookingId);
        }

        // Send confirmation email to mentor
        try
        {
            if (string.IsNullOrWhiteSpace(booking.Mentor?.Email))
            {
                _logger.LogWarning("📧 Mentor email is null/empty for BookingId={BookingId}. Skipping mentor email.", notification.BookingId);
            }
            else
            {
                _logger.LogInformation("📧 Sending booking_confirmed_mentor to mentor: {Email}", booking.Mentor.Email);
                await _emailService.SendTemplatedEmailAsync(
                    EmailTemplateKeys.BookingConfirmedMentor,
                    booking.Mentor.Email,
                    new Dictionary<string, string>
                    {
                        ["studentName"] = booking.Student.DisplayName,
                        ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                        ["bookingTime"] = booking.StartAt.ToString("HH:mm"),
                        ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                    },
                    cancellationToken);
                _logger.LogInformation("📧 ✅ booking_confirmed_mentor email sent to mentor {Email}", booking.Mentor.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "📧 ❌ Failed to send booking_confirmed_mentor email to mentor for BookingId={BookingId}", notification.BookingId);
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