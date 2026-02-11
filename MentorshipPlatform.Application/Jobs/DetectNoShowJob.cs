using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class DetectNoShowJob
{
    private readonly IApplicationDbContext _context;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<DetectNoShowJob> _logger;
    private const int GRACE_PERIOD_MINUTES = 15;
    private const double MIN_ATTENDANCE_RATIO = 0.25; // 25% of scheduled duration

    public DetectNoShowJob(
        IApplicationDbContext context,
        IProcessHistoryService history,
        ILogger<DetectNoShowJob> logger)
    {
        _context = context;
        _history = history;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-GRACE_PERIOD_MINUTES);

            // Find confirmed bookings whose end time + grace period has passed
            // but weren't completed by the mentor
            var overdueBookings = await _context.Bookings
                .Where(b => b.Status == BookingStatus.Confirmed && b.EndAt < cutoff)
                .ToListAsync();

            if (!overdueBookings.Any()) return;

            _logger.LogInformation("🔍 Checking {Count} overdue bookings for no-show", overdueBookings.Count);

            foreach (var booking in overdueBookings)
            {
                try
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
                    string noShowReason;

                    if (!mentorJoined && !studentJoined)
                    {
                        // Neither joined
                        noShowReason = "Mentor ve öğrenci seansa katılmadı";
                    }
                    else if (!mentorJoined)
                    {
                        noShowReason = "Mentor seansa katılmadı";
                    }
                    else if (!studentJoined)
                    {
                        noShowReason = "Öğrenci seansa katılmadı";
                    }
                    else if (mentorDurationSec < minDurationSec && studentDurationSec < minDurationSec)
                    {
                        noShowReason = $"Her iki taraf da yeterli süre katılmadı (Mentor: {mentorDurationSec}sn, Öğrenci: {studentDurationSec}sn, Minimum: {minDurationSec:F0}sn)";
                    }
                    else
                    {
                        // Both participated sufficiently - auto-complete the booking
                        booking.Complete();
                        await _context.SaveChangesAsync();

                        await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                            "Confirmed", "Completed",
                            $"Randevu süresi doldu, yeterli katılım tespit edildi. Otomatik tamamlandı. (Mentor: {mentorDurationSec}sn, Öğrenci: {studentDurationSec}sn)",
                            performedByRole: "System");

                        continue;
                    }

                    // Mark as no-show
                    booking.MarkAsNoShow();
                    await _context.SaveChangesAsync();

                    var metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        mentorJoined,
                        studentJoined,
                        mentorDurationSec,
                        studentDurationSec,
                        scheduledDurationSec,
                        sessionExists = session != null
                    });

                    await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                        "Confirmed", "NoShow",
                        $"NoShow tespit edildi: {noShowReason}",
                        performedByRole: "System",
                        metadata: metadata);

                    _logger.LogWarning("⚠️ NoShow detected: Booking {BookingId} - {Reason}",
                        booking.Id, noShowReason);
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
}
