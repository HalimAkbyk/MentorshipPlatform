using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class PaymentReconciliationJob
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IProcessHistoryService _history;
    private readonly ILogger<PaymentReconciliationJob> _logger;
    private readonly IPlatformSettingService _settings;

    public PaymentReconciliationJob(
        IApplicationDbContext context,
        IPaymentService paymentService,
        IProcessHistoryService history,
        ILogger<PaymentReconciliationJob> logger,
        IPlatformSettingService settings)
    {
        _context = context;
        _paymentService = paymentService;
        _history = history;
        _logger = logger;
        _settings = settings;
    }

    public async Task Execute()
    {
        try
        {
            // Find orders that are pending for more than 10 minutes but less than 24 hours
            // These might have been paid on Iyzico but webhook failed
            var minAge = DateTime.UtcNow.AddMinutes(-10);
            var maxAge = DateTime.UtcNow.AddHours(-24);

            var stuckOrders = await _context.Orders
                .Where(o => o.Status == OrderStatus.Pending
                    && o.CreatedAt < minAge
                    && o.CreatedAt > maxAge
                    && o.CheckoutToken != null)
                .ToListAsync();

            if (!stuckOrders.Any()) return;

            _logger.LogInformation("🔄 Reconciliation: Checking {Count} stuck orders", stuckOrders.Count);

            foreach (var order in stuckOrders)
            {
                try
                {
                    var verification = await _paymentService.VerifyPaymentAsync(order.CheckoutToken!);

                    if (verification.IsSuccess)
                    {
                        // Payment was successful on Iyzico but we missed the webhook
                        _logger.LogWarning(
                            "⚠️ Reconciliation: Order {OrderId} was paid on Iyzico but not in our system!",
                            order.Id);

                        order.MarkAsPaid("Iyzico", verification.ProviderPaymentId);

                        await _history.LogAsync("Order", order.Id, "Reconciled",
                            "Pending", "Paid",
                            "Ödeme uzlaştırma: Iyzico'da başarılı ödeme tespit edildi, sistem güncellendi",
                            performedByRole: "System",
                            metadata: $"{{\"providerPaymentId\":\"{verification.ProviderPaymentId}\"}}");

                        // Confirm booking/enrollment
                        if (order.Type == OrderType.Booking)
                        {
                            var booking = await _context.Bookings
                                .Include(b => b.Offering)
                                .FirstOrDefaultAsync(b => b.Id == order.ResourceId
                                    && b.Status == BookingStatus.PendingPayment);

                            if (booking != null)
                            {
                                booking.Confirm();

                                // Mark slot as booked
                                var slot = await _context.AvailabilitySlots
                                    .FirstOrDefaultAsync(s =>
                                        s.MentorUserId == booking.MentorUserId &&
                                        s.StartAt <= booking.StartAt &&
                                        s.EndAt >= booking.EndAt &&
                                        !s.IsBooked);
                                slot?.MarkAsBooked();

                                // Create ledger entries (coupon-aware split)
                                var commissionRate = await _settings.GetDecimalAsync(
                                    PlatformSettings.MentorCommissionRate, 0.15m);

                                decimal mentorNet, platformCommission;
                                bool isAdminCoupon = order.DiscountAmount > 0
                                    && string.Equals(order.CouponCreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase);

                                if (isAdminCoupon)
                                {
                                    var originalPrice = order.AmountTotal + order.DiscountAmount;
                                    mentorNet = originalPrice * (1 - commissionRate);
                                    platformCommission = order.AmountTotal - mentorNet;
                                }
                                else
                                {
                                    mentorNet = order.AmountTotal * (1 - commissionRate);
                                    platformCommission = order.AmountTotal * commissionRate;
                                }

                                _context.LedgerEntries.Add(LedgerEntry.Create(
                                    LedgerAccountType.MentorEscrow,
                                    LedgerDirection.Credit,
                                    mentorNet,
                                    order.Type.ToString(),
                                    order.Id,
                                    booking.MentorUserId));

                                _context.LedgerEntries.Add(LedgerEntry.Create(
                                    LedgerAccountType.Platform,
                                    LedgerDirection.Credit,
                                    platformCommission,
                                    order.Type.ToString(),
                                    order.Id));

                                await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                                    "PendingPayment", "Confirmed",
                                    "Ödeme uzlaştırma sonucu otomatik onay",
                                    performedByRole: "System");
                            }
                        }

                        await _context.SaveChangesAsync();
                    }
                    // If verification returns not success, it could just mean payment hasn't completed yet
                    // Don't mark as failed - the ExpirePendingOrdersJob will handle that after 30 min
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "❌ Reconciliation error for order {OrderId}", order.Id);
                }
            }

            _logger.LogInformation("✅ Reconciliation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in PaymentReconciliationJob");
        }
    }
}
