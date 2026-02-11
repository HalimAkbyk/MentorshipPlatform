using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class ExpirePendingOrdersJob
{
    private readonly IApplicationDbContext _context;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<ExpirePendingOrdersJob> _logger;
    private const int EXPIRY_MINUTES = 30;

    public ExpirePendingOrdersJob(
        IApplicationDbContext context,
        IProcessHistoryService history,
        ILogger<ExpirePendingOrdersJob> logger)
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

            var expiredOrders = await _context.Orders
                .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
                .ToListAsync();

            if (!expiredOrders.Any()) return;

            _logger.LogInformation("⏰ Found {Count} expired pending orders", expiredOrders.Count);

            foreach (var order in expiredOrders)
            {
                order.MarkAsFailed();

                await _history.LogAsync("Order", order.Id, "StatusChanged",
                    "Pending", "Failed",
                    $"Sipariş {EXPIRY_MINUTES} dakika içinde ödenmedi, otomatik iptal edildi",
                    performedByRole: "System");

                // If this is a booking order, expire the booking and release the slot
                if (order.Type == OrderType.Booking)
                {
                    var booking = await _context.Bookings
                        .FirstOrDefaultAsync(b => b.Id == order.ResourceId
                            && b.Status == BookingStatus.PendingPayment);

                    if (booking != null)
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
                            "Ödeme süresi doldu, randevu otomatik iptal edildi. Slot serbest bırakıldı.",
                            performedByRole: "System");
                    }
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("✅ Expired {Count} pending orders", expiredOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in ExpirePendingOrdersJob");
        }
    }
}
