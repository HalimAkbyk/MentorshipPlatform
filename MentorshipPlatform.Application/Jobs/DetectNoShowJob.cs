using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

/// <summary>
/// Recurring job (every 10 minutes): Detects directional no-shows
/// after EndAt + noShowWaitMinutes has passed.
///
/// Scenarios:
///   - Neither joined → generic NoShow
///   - Student didn't join (mentor did) → StudentNoShow → mentor keeps payment
///   - Mentor didn't join (student did) → MentorNoShow → student auto-refund
///   - Both joined but below MIN_ATTENDANCE → generic NoShow
///   - Both joined sufficiently → auto-complete
/// </summary>
public class DetectNoShowJob
{
    private readonly IApplicationDbContext _context;
    private readonly IPlatformSettingService _settings;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<DetectNoShowJob> _logger;
    private const double MIN_ATTENDANCE_RATIO = 0.25; // 25% of scheduled duration

    public DetectNoShowJob(
        IApplicationDbContext context,
        IPlatformSettingService settings,
        IProcessHistoryService history,
        ILogger<DetectNoShowJob> logger)
    {
        _context = context;
        _settings = settings;
        _history = history;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var noShowWait = await _settings.GetIntAsync(PlatformSettings.NoShowWaitMinutes, 15);
            var cutoff = DateTime.UtcNow.AddMinutes(-noShowWait);

            // Find confirmed bookings whose end time + grace period has passed
            // but weren't completed by the mentor
            var overdueBookings = await _context.Bookings
                .Include(b => b.Student)
                .Include(b => b.Mentor)
                .Include(b => b.Offering)
                .Where(b => b.Status == BookingStatus.Confirmed && b.EndAt < cutoff)
                .ToListAsync();

            if (!overdueBookings.Any()) return;

            _logger.LogInformation("🔍 Checking {Count} overdue bookings for no-show", overdueBookings.Count);

            foreach (var booking in overdueBookings)
            {
                try
                {
                    await ProcessBooking(booking, noShowWait);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing booking {BookingId} for no-show", booking.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in DetectNoShowJob");
        }
    }

    private async Task ProcessBooking(Booking booking, int noShowWait)
    {
        // Check video session participation
        var session = await _context.VideoSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s =>
                s.ResourceType == "Booking" &&
                s.ResourceId == booking.Id);

        var mentorJoined = false;
        var studentJoined = false;
        var mentorDurationSec = 0;
        var studentDurationSec = 0;

        if (session != null)
        {
            foreach (var p in session.Participants)
            {
                if (p.UserId == booking.MentorUserId)
                {
                    mentorJoined = true;
                    mentorDurationSec += p.DurationSec;
                }
                else if (p.UserId == booking.StudentUserId)
                {
                    studentJoined = true;
                    studentDurationSec += p.DurationSec;
                }
            }
        }

        var scheduledDurationSec = booking.DurationMin * 60;
        var minDurationSec = scheduledDurationSec * MIN_ATTENDANCE_RATIO;

        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            mentorJoined,
            studentJoined,
            mentorDurationSec,
            studentDurationSec,
            scheduledDurationSec,
            sessionExists = session != null
        });

        // ── Scenario: Both participated sufficiently → auto-complete ──
        if (mentorJoined && studentJoined
            && mentorDurationSec >= minDurationSec && studentDurationSec >= minDurationSec)
        {
            booking.Complete();
            await _context.SaveChangesAsync();

            await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                "Confirmed", "Completed",
                $"Randevu süresi doldu, yeterli katılım tespit edildi. Otomatik tamamlandı. (Mentor: {mentorDurationSec}sn, Öğrenci: {studentDurationSec}sn)",
                performedByRole: "System", metadata: metadata);

            return;
        }

        // ── Scenario: Student no-show (mentor came, student didn't) ──
        if (mentorJoined && !studentJoined)
        {
            booking.MarkAsStudentNoShow();
            await _context.SaveChangesAsync();

            await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                "Confirmed", "StudentNoShow",
                $"Öğrenci seansa katılmadı. Mentor {mentorDurationSec}sn katıldı. Bekleme süresi: {noShowWait}dk.",
                performedByRole: "System", metadata: metadata);

            // Notify mentor — payment will be processed
            _context.UserNotifications.Add(UserNotification.Create(
                booking.MentorUserId, "StudentNoShow",
                "Öğrenci Katılmadı",
                $"Öğrenciniz \"{booking.Student.DisplayName}\" randevuya katılmadı. Ödemeniz işlenecektir.",
                "Booking", booking.Id));

            // Notify student
            _context.UserNotifications.Add(UserNotification.Create(
                booking.StudentUserId, "StudentNoShow",
                "Randevuya Katılmadınız",
                $"Mentor \"{booking.Mentor.DisplayName}\" ile randevunuza katılmadınız. Ödeme mentora aktarılacaktır.",
                "Booking", booking.Id));

            await _context.SaveChangesAsync();

            _logger.LogWarning("⚠️ StudentNoShow: Booking {BookingId} — student didn't join", booking.Id);
            return;
        }

        // ── Scenario: Mentor no-show (student came, mentor didn't) → auto-refund ──
        if (!mentorJoined && studentJoined)
        {
            booking.MarkAsMentorNoShow();
            await _context.SaveChangesAsync();

            await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                "Confirmed", "MentorNoShow",
                $"Mentor seansa katılmadı. Öğrenci {studentDurationSec}sn katıldı. Bekleme süresi: {noShowWait}dk.",
                performedByRole: "System", metadata: metadata);

            // Create auto-refund request for the student
            await CreateNoShowRefundRequest(booking);

            // Notify student — refund coming
            _context.UserNotifications.Add(UserNotification.Create(
                booking.StudentUserId, "MentorNoShow",
                "Mentor Katılmadı — İade İşlemi Başlatıldı",
                $"Mentor \"{booking.Mentor.DisplayName}\" randevuya katılmadı. Ödemeniz için iade talebi otomatik olarak oluşturuldu.",
                "Booking", booking.Id));

            // Notify mentor
            _context.UserNotifications.Add(UserNotification.Create(
                booking.MentorUserId, "MentorNoShow",
                "Randevuya Katılmadınız",
                $"Öğrenciniz \"{booking.Student.DisplayName}\" ile randevunuza katılmadınız. Öğrenciye iade yapılacaktır.",
                "Booking", booking.Id));

            await _context.SaveChangesAsync();

            _logger.LogWarning("⚠️ MentorNoShow: Booking {BookingId} — mentor didn't join, auto-refund created", booking.Id);
            return;
        }

        // ── Scenario: Neither joined or both insufficient attendance → generic NoShow ──
        string noShowReason;
        if (!mentorJoined && !studentJoined)
        {
            noShowReason = "Mentor ve öğrenci seansa katılmadı";
        }
        else
        {
            noShowReason = $"Her iki taraf da yeterli süre katılmadı (Mentor: {mentorDurationSec}sn, Öğrenci: {studentDurationSec}sn, Minimum: {minDurationSec:F0}sn)";
        }

        booking.MarkAsNoShow();
        await _context.SaveChangesAsync();

        await _history.LogAsync("Booking", booking.Id, "StatusChanged",
            "Confirmed", "NoShow",
            $"NoShow tespit edildi: {noShowReason}",
            performedByRole: "System", metadata: metadata);

        // For generic no-show (neither came) → refund student
        await CreateNoShowRefundRequest(booking);

        // Notify both parties
        _context.UserNotifications.Add(UserNotification.Create(
            booking.StudentUserId, "NoShow",
            "Randevu Gerçekleşmedi",
            $"Mentor \"{booking.Mentor.DisplayName}\" ile randevunuz gerçekleşmedi. İade talebi otomatik oluşturuldu.",
            "Booking", booking.Id));

        _context.UserNotifications.Add(UserNotification.Create(
            booking.MentorUserId, "NoShow",
            "Randevu Gerçekleşmedi",
            $"Öğrenciniz \"{booking.Student.DisplayName}\" ile randevunuz gerçekleşmedi.",
            "Booking", booking.Id));

        await _context.SaveChangesAsync();

        _logger.LogWarning("⚠️ NoShow detected: Booking {BookingId} - {Reason}", booking.Id, noShowReason);
    }

    /// <summary>
    /// Creates an automatic refund request for no-show scenarios where the student deserves a refund.
    /// </summary>
    private async Task CreateNoShowRefundRequest(Booking booking)
    {
        try
        {
            // Find the paid order for this booking
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.ResourceId == booking.Id &&
                    o.Type == OrderType.Booking &&
                    o.Status == OrderStatus.Paid);

            if (order == null)
            {
                _logger.LogWarning("No paid order found for booking {BookingId} — skipping auto-refund", booking.Id);
                return;
            }

            // Check if a refund request already exists
            var existingRefund = await _context.RefundRequests
                .AnyAsync(r => r.OrderId == order.Id);

            if (existingRefund)
            {
                _logger.LogInformation("Refund request already exists for order {OrderId} — skipping", order.Id);
                return;
            }

            var refundRequest = RefundRequest.Create(
                order.Id,
                booking.StudentUserId,
                "Otomatik iade: No-show tespit edildi",
                order.AmountTotal,
                RefundType.NoShowRefund);

            _context.RefundRequests.Add(refundRequest);
            await _context.SaveChangesAsync();

            await _history.LogAsync("RefundRequest", refundRequest.Id, "Created",
                null, "Pending",
                $"No-show nedeniyle otomatik iade talebi oluşturuldu. Tutar: {order.AmountTotal} {order.Currency}",
                performedByRole: "System");

            _logger.LogInformation("📋 Auto-refund request created for booking {BookingId}, amount: {Amount}",
                booking.Id, order.AmountTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating auto-refund for booking {BookingId}", booking.Id);
        }
    }
}
