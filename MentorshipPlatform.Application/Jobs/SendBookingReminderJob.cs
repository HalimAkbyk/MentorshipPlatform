using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class SendBookingReminderJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendBookingReminderJob> _logger;

    public SendBookingReminderJob(
        IApplicationDbContext context,
        IEmailService emailService,
        ISmsService smsService,
        IConfiguration configuration,
        ILogger<SendBookingReminderJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _smsService = smsService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Execute(Guid bookingId, string timeframe)
    {
        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.Status != BookingStatus.Confirmed)
            return;

        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        var frontendUrl = _configuration["Frontend:BaseUrl"]
                          ?? _configuration["FrontendUrl"]
                          ?? "http://localhost:3000";

        // Send email reminder to student
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.BookingReminder,
                booking.Student.Email!,
                new Dictionary<string, string>
                {
                    ["otherPartyName"] = booking.Mentor.DisplayName,
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["bookingTime"] = booking.StartAt.ToString("HH:mm"),
                    ["timeframe"] = timeframe,
                    ["classroomUrl"] = $"{frontendUrl}/student/classroom/{bookingId}",
                    ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking reminder email to student for {BookingId}", bookingId);
        }

        // Send SMS for 10-minute reminder only
        if (timeframe == "10m" && !string.IsNullOrEmpty(booking.Student.Phone))
        {
            await _smsService.SendBookingReminderSmsAsync(
                booking.Student.Phone,
                booking.Mentor.DisplayName,
                booking.StartAt);
        }

        // Also remind mentor
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.BookingReminder,
                booking.Mentor.Email!,
                new Dictionary<string, string>
                {
                    ["otherPartyName"] = booking.Student.DisplayName,
                    ["bookingDate"] = booking.StartAt.ToString("dd MMMM yyyy", trCulture),
                    ["bookingTime"] = booking.StartAt.ToString("HH:mm"),
                    ["timeframe"] = timeframe,
                    ["classroomUrl"] = $"{frontendUrl}/mentor/classroom/{bookingId}",
                    ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking reminder email to mentor for {BookingId}", bookingId);
        }
    }
}