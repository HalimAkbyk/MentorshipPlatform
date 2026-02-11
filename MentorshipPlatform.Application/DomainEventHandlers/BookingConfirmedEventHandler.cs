using Hangfire;
using MediatR;
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
            .FirstOrDefaultAsync(b => b.Id == notification.BookingId, cancellationToken);

        if (booking == null) return;

        // Send confirmation email
        await _emailService.SendBookingConfirmationAsync(
            booking.Student.Email!,
            booking.Mentor.DisplayName,
            booking.StartAt,
            cancellationToken);

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