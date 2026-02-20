using Hangfire;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Jobs;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Payments.Commands.ProcessPaymentWebhook;

public record ProcessPaymentWebhookCommand(
    string Token) : IRequest<Result>;

public class ProcessPaymentWebhookCommandHandler : IRequestHandler<ProcessPaymentWebhookCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IProcessHistoryService _history;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<ProcessPaymentWebhookCommandHandler> _logger;
    private readonly IPlatformSettingService _settings;
    private readonly IAdminNotificationService _adminNotification;

    public ProcessPaymentWebhookCommandHandler(
        IApplicationDbContext context,
        IPaymentService paymentService,
        IProcessHistoryService history,
        IBackgroundJobClient backgroundJobs,
        ILogger<ProcessPaymentWebhookCommandHandler> logger,
        IPlatformSettingService settings,
        IAdminNotificationService adminNotification)
    {
        _context = context;
        _paymentService = paymentService;
        _history = history;
        _backgroundJobs = backgroundJobs;
        _settings = settings;
        _logger = logger;
        _adminNotification = adminNotification;
    }

    public async Task<Result> Handle(
        ProcessPaymentWebhookCommand request,
        CancellationToken cancellationToken)
    {
        Order? order = null;

        try
        {
            // Step 1: Verify payment
            var verification = await _paymentService.VerifyPaymentAsync(request.Token);

            if (!verification.IsSuccess)
            {
                _logger.LogError("❌ Payment verification failed for token: {Token}", request.Token);
                return Result<bool>.Failure("Verification failed");
            }

            // Step 2: Parse Order.Id
            if (!Guid.TryParse(verification.OrderId, out var orderId))
            {
                _logger.LogError("❌ Invalid OrderId in verification: {OrderId}", verification.OrderId);
                return Result<bool>.Failure("Invalid ConversationId");
            }

            // Step 3: Find order
            order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null)
            {
                _logger.LogError("❌ Order not found: {OrderId}", orderId);
                return Result<bool>.Failure("Order not found");
            }

            // Step 4: Idempotency check
            if (order.Status == OrderStatus.Paid)
            {
                _logger.LogInformation("ℹ️ Order already processed: {OrderId}", orderId);
                return Result<bool>.Success(true);
            }

            // Step 5: Mark as paid
            order.MarkAsPaid("Iyzico", verification.ProviderPaymentId, verification.ProviderTransactionId);

            await _history.LogAsync("Order", order.Id, "StatusChanged",
                "Pending", "Paid",
                $"Iyzico ödeme doğrulandı. ProviderPaymentId: {verification.ProviderPaymentId}, ProviderTransactionId: {verification.ProviderTransactionId}",
                order.BuyerUserId, "Student", ct: cancellationToken);

            // Update booking/enrollment status
            Guid mentorUserId;
            if (order.Type == OrderType.Booking)
            {
                var booking = await _context.Bookings
                    .Include(b => b.Offering)
                    .FirstOrDefaultAsync(b => b.Id == order.ResourceId, cancellationToken);

                if (booking == null)
                {
                    _logger.LogError("❌ Related booking not found: {ResourceId}", order.ResourceId);
                    await _history.LogAsync("Order", order.Id, "Error",
                        null, null,
                        $"İlişkili booking bulunamadı: {order.ResourceId}",
                        metadata: $"{{\"resourceId\":\"{order.ResourceId}\"}}", ct: cancellationToken);
                    return Result<bool>.Failure("Related booking not found");
                }

                booking.Confirm();
                mentorUserId = booking.MentorUserId;

                await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                    "PendingPayment", "Confirmed",
                    "Ödeme sonrası otomatik onay",
                    performedByRole: "System", ct: cancellationToken);

                // Mark slot as booked
                var slot = await _context.AvailabilitySlots
                    .FirstOrDefaultAsync(s =>
                        s.MentorUserId == mentorUserId &&
                        s.StartAt <= booking.StartAt &&
                        s.EndAt >= booking.EndAt &&
                        !s.IsBooked,
                        cancellationToken);
                slot?.MarkAsBooked();
            }
            else if (order.Type == OrderType.Course)
            {
                var courseEnrollment = await _context.CourseEnrollments
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.Id == order.ResourceId, cancellationToken);

                if (courseEnrollment == null)
                {
                    _logger.LogError("❌ Related course enrollment not found: {ResourceId}", order.ResourceId);
                    return Result<bool>.Failure("Related course enrollment not found");
                }

                courseEnrollment.Confirm();
                courseEnrollment.Course.IncrementEnrollmentCount();
                mentorUserId = courseEnrollment.Course.MentorUserId;

                await _history.LogAsync("CourseEnrollment", courseEnrollment.Id, "StatusChanged",
                    "PendingPayment", "Active",
                    "Ödeme sonrası kurs erişimi aktifleştirildi",
                    performedByRole: "System", ct: cancellationToken);
            }
            else
            {
                var enrollment = await _context.ClassEnrollments
                    .Include(e => e.Class)
                    .FirstOrDefaultAsync(e => e.Id == order.ResourceId, cancellationToken);

                if (enrollment == null)
                {
                    _logger.LogError("❌ Related enrollment not found: {ResourceId}", order.ResourceId);
                    return Result<bool>.Failure("Related enrollment not found");
                }

                enrollment.Confirm();
                mentorUserId = enrollment.Class.MentorUserId;
            }

            // Create ledger entries (escrow model)
            // Coupon-aware split:
            //   Admin coupon  → discount comes from platform's share. Mentor earns as if no discount.
            //   Mentor coupon → standard split on actual paid amount. Mentor bears the discount.
            //   No coupon     → standard split.
            decimal mentorNet, platformCommission;
            bool isAdminCoupon = order.DiscountAmount > 0
                                 && string.Equals(order.CouponCreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase);

            var commissionRate = await _settings.GetDecimalAsync(
                PlatformSettings.MentorCommissionRate, 0.15m, cancellationToken);

            if (isAdminCoupon)
            {
                // Original price before discount
                var originalPrice = order.AmountTotal + order.DiscountAmount;
                mentorNet = originalPrice * (1 - commissionRate);
                platformCommission = order.AmountTotal - mentorNet; // may be negative (platform promotional cost)
            }
            else
            {
                // Standard calculation (mentor coupon or no coupon)
                mentorNet = order.AmountTotal * (1 - commissionRate);
                platformCommission = order.AmountTotal * commissionRate;
            }

            _context.LedgerEntries.Add(LedgerEntry.Create(
                LedgerAccountType.MentorEscrow,
                LedgerDirection.Credit,
                mentorNet,
                order.Type.ToString(),
                order.Id,
                mentorUserId));

            _context.LedgerEntries.Add(LedgerEntry.Create(
                LedgerAccountType.Platform,
                LedgerDirection.Credit,
                platformCommission,
                order.Type.ToString(),
                order.Id));

            await _context.SaveChangesAsync(cancellationToken);

            // Schedule course payout after 7-day refund window
            if (order.Type == OrderType.Course)
            {
                var coursePayoutTime = DateTime.UtcNow.AddDays(7);
                _backgroundJobs.Schedule<ProcessCoursePayoutJob>(
                    job => job.Execute(order.Id),
                    coursePayoutTime);

                _logger.LogInformation(
                    "📅 Scheduled course payout for Order {OrderId} at {PayoutTime}",
                    order.Id, coursePayoutTime);
            }

            _logger.LogInformation("✅ Payment processed successfully for Order: {OrderId}", order.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception in ProcessPaymentWebhookCommand for token: {Token}", request.Token);

            // Try to mark order as failed if we have it
            if (order != null && order.Status == OrderStatus.Pending)
            {
                try
                {
                    order.MarkAsFailed();
                    await _context.SaveChangesAsync(cancellationToken);

                    await _history.LogAsync("Order", order.Id, "PaymentProcessingFailed",
                        "Pending", "Failed",
                        $"Ödeme işleme sırasında hata: {ex.Message}",
                        performedByRole: "System",
                        metadata: $"{{\"exception\":\"{ex.GetType().Name}\",\"message\":\"{ex.Message.Replace("\"", "'")}\"}}",
                        ct: cancellationToken);

                    // Admin notification for payment failure
                    try
                    {
                        await _adminNotification.CreateOrUpdateGroupedAsync(
                            "PaymentFailed",
                            "payment-failures",
                            count => ("Ödeme Hataları", $"{count} başarısız ödeme işlemi var"),
                            "Order", order.Id,
                            cancellationToken);
                    }
                    catch { /* don't fail the main flow for notification errors */ }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "❌ Failed to mark order as failed: {OrderId}", order.Id);
                }
            }

            return Result<bool>.Failure($"Payment processing error: {ex.Message}");
        }
    }
}
