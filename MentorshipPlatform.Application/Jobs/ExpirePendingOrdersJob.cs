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
    private readonly IPlatformSettingService _settings;

    public ExpirePendingOrdersJob(
        IApplicationDbContext context,
        IProcessHistoryService history,
        ILogger<ExpirePendingOrdersJob> logger,
        IPlatformSettingService settings)
    {
        _context = context;
        _history = history;
        _logger = logger;
        _settings = settings;
    }

    public async Task Execute()
    {
        try
        {
            var expiryMinutes = await _settings.GetIntAsync(
                PlatformSettings.BookingAutoExpireMinutes, 30);
            var cutoff = DateTime.UtcNow.AddMinutes(-expiryMinutes);

            var expiredOrders = await _context.Orders
                .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
                .ToListAsync();

            if (!expiredOrders.Any()) return;

            _logger.LogInformation("⏰ Found {Count} expired pending orders", expiredOrders.Count);

            foreach (var order in expiredOrders)
            {
                // Distinguish between abandoned (user closed popup) vs actual payment failure
                // If ProviderPaymentId is null, Iyzico never received a payment attempt
                var isAbandoned = string.IsNullOrEmpty(order.ProviderPaymentId);

                if (isAbandoned)
                    order.MarkAsAbandoned();
                else
                    order.MarkAsFailed();

                var newStatus = isAbandoned ? "Abandoned" : "Failed";
                var description = isAbandoned
                    ? $"Kullanıcı ödeme yapmadan vazgeçti ({expiryMinutes} dk süre doldu)"
                    : $"Sipariş {expiryMinutes} dakika içinde ödenmedi, otomatik iptal edildi";

                await _history.LogAsync("Order", order.Id, "StatusChanged",
                    "Pending", newStatus,
                    description,
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
                // If this is a group class enrollment, cancel the enrollment
                else if (order.Type == OrderType.GroupClass)
                {
                    var enrollment = await _context.ClassEnrollments
                        .FirstOrDefaultAsync(e => e.Id == order.ResourceId
                            && e.Status == EnrollmentStatus.PendingPayment);

                    if (enrollment != null)
                    {
                        enrollment.Cancel();

                        await _history.LogAsync("ClassEnrollment", enrollment.Id, "StatusChanged",
                            "PendingPayment", "Cancelled",
                            "Ödeme süresi doldu, grup dersi kaydı otomatik iptal edildi.",
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
