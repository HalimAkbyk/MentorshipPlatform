using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class ExpirePendingBookingsJob
{
    private readonly IApplicationDbContext _context;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<ExpirePendingBookingsJob> _logger;
    private const int EXPIRY_MINUTES = 30;

    public ExpirePendingBookingsJob(
        IApplicationDbContext context,
        IProcessHistoryService history,
        ILogger<ExpirePendingBookingsJob> logger)
    {
        _context = context;
        _history = history;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-EXPIRY_MINUTES);

            var expiredBookings = await _context.Bookings
                .Where(b => b.Status == BookingStatus.PendingPayment && b.CreatedAt < cutoff)
                .ToListAsync();

            if (!expiredBookings.Any()) return;

            _logger.LogInformation("⏰ Found {Count} expired pending bookings", expiredBookings.Count);

            foreach (var booking in expiredBookings)
            {
                booking.MarkAsExpired();

                // Release availability slot
                var slot = await _context.AvailabilitySlots
                    .FirstOrDefaultAsync(s =>
                        s.MentorUserId == booking.MentorUserId &&
                        s.IsBooked &&
                        s.StartAt <= booking.StartAt &&
                        s.EndAt >= booking.EndAt);
                slot?.MarkAsAvailable();

                await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                    "PendingPayment", "Expired",
                    $"Randevu {EXPIRY_MINUTES} dakika içinde ödenmedi, otomatik iptal edildi",
                    performedByRole: "System");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("✅ Expired {Count} pending bookings", expiredBookings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in ExpirePendingBookingsJob");
        }
    }
}
