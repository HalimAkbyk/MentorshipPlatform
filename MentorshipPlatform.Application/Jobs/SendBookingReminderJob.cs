using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Jobs;

public class SendBookingReminderJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;

    public SendBookingReminderJob(
        IApplicationDbContext context,
        IEmailService emailService,
        ISmsService smsService)
    {
        _context = context;
        _emailService = emailService;
        _smsService = smsService;
    }

    public async Task Execute(Guid bookingId, string timeframe)
    {
        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.Status != BookingStatus.Confirmed)
            return;

        // Send email reminder
        await _emailService.SendBookingReminderAsync(
            booking.Student.Email!,
            booking.Mentor.DisplayName,
            booking.StartAt,
            timeframe);

        // Send SMS for 10-minute reminder only
        if (timeframe == "10m" && !string.IsNullOrEmpty(booking.Student.Phone))
        {
            await _smsService.SendBookingReminderSmsAsync(
                booking.Student.Phone,
                booking.Mentor.DisplayName,
                booking.StartAt);
        }

        // Also remind mentor
        await _emailService.SendBookingReminderAsync(
            booking.Mentor.Email!,
            booking.Student.DisplayName,
            booking.StartAt,
            timeframe);
    }
}