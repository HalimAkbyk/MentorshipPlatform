using Hangfire;
using MediatR;
using MentorshipPlatform.Application.Common.Constants;
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
    private readonly IEmailService _emailService;

    public ProcessPaymentWebhookCommandHandler(
        IApplicationDbContext context,
        IPaymentService paymentService,
        IProcessHistoryService history,
        IBackgroundJobClient backgroundJobs,
        ILogger<ProcessPaymentWebhookCommandHandler> logger,
        IPlatformSettingService settings,
        IAdminNotificationService adminNotification,
        IEmailService emailService)
    {
        _context = context;
        _paymentService = paymentService;
        _history = history;
        _backgroundJobs = backgroundJobs;
        _settings = settings;
        _logger = logger;
        _adminNotification = adminNotification;
        _emailService = emailService;
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

            // Handle Package purchase early — no mentor, no escrow
            if (order.Type == OrderType.Package)
            {
                var package = await _context.Packages
                    .FirstOrDefaultAsync(p => p.Id == order.ResourceId, cancellationToken);

                if (package == null)
                {
                    _logger.LogError("Related package not found: {ResourceId}", order.ResourceId);
                    return Result<bool>.Failure("Related package not found");
                }

                // Create PackagePurchase record
                var purchase = PackagePurchase.Create(
                    order.BuyerUserId,
                    package.Id,
                    order.AmountTotal,
                    order.Id);
                _context.PackagePurchases.Add(purchase);
                await _context.SaveChangesAsync(cancellationToken); // save to get purchase.Id

                await _history.LogAsync("PackagePurchase", purchase.Id, "Created",
                    null, "Completed",
                    $"Paket satın alındı: {package.Name}, Tutar: {order.AmountTotal} TRY",
                    order.BuyerUserId, "Student", ct: cancellationToken);

                // Calculate expiry date
                DateTime? expiresAt = package.ValidityDays.HasValue
                    ? DateTime.UtcNow.AddDays(package.ValidityDays.Value)
                    : null;

                // Create StudentCredit records for each credit type (only if credits > 0)
                var creditEntries = new List<(CreditType type, int amount)>
                {
                    (CreditType.PrivateLesson, package.PrivateLessonCredits),
                    (CreditType.GroupLesson, package.GroupLessonCredits),
                    (CreditType.VideoAccess, package.VideoAccessCredits)
                };

                foreach (var (creditType, creditAmount) in creditEntries)
                {
                    if (creditAmount <= 0) continue;

                    var credit = StudentCredit.Create(
                        order.BuyerUserId,
                        purchase.Id,
                        creditType,
                        creditAmount,
                        expiresAt);
                    _context.StudentCredits.Add(credit);
                    await _context.SaveChangesAsync(cancellationToken); // save to get credit.Id

                    var transaction = CreditTransaction.Create(
                        credit.Id,
                        CreditTransactionType.Purchase,
                        creditAmount,
                        relatedEntityId: purchase.Id,
                        relatedEntityType: "PackagePurchase",
                        description: $"{package.Name} paketi ile {creditAmount} {creditType} kredisi eklendi.");
                    _context.CreditTransactions.Add(transaction);
                }

                // Package purchases: all revenue goes to platform
                _context.LedgerEntries.Add(LedgerEntry.Create(
                    LedgerAccountType.Platform,
                    LedgerDirection.Credit,
                    order.AmountTotal,
                    order.Type.ToString(),
                    order.Id));

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Package purchase processed successfully for Order: {OrderId}, Package: {PackageName}",
                    order.Id, package.Name);
                return Result.Success();
            }

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
                _logger.LogInformation("📧 Booking.Confirm() called — domain event raised for BookingId={BookingId}. Email will be dispatched on SaveChanges.", booking.Id);

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

                // Send course enrollment email
                try
                {
                    var studentUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == order.BuyerUserId, cancellationToken);
                    var mentorUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == mentorUserId, cancellationToken);

                    _logger.LogInformation("📧 Course enrollment: StudentEmail={Email}, Course={Course}",
                        studentUser?.Email ?? "NULL", courseEnrollment.Course.Title);

                    if (studentUser?.Email != null)
                    {
                        await _emailService.SendTemplatedEmailAsync(
                            EmailTemplateKeys.CourseEnrolled,
                            studentUser.Email,
                            new Dictionary<string, string>
                            {
                                ["courseTitle"] = courseEnrollment.Course.Title,
                                ["mentorName"] = mentorUser?.DisplayName ?? "Mentor"
                            },
                            cancellationToken);
                        _logger.LogInformation("📧 ✅ course_enrolled email sent to {Email}", studentUser.Email);
                    }
                    else
                    {
                        _logger.LogWarning("📧 Student email is null for order {OrderId}. Cannot send course enrollment email.", order.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "📧 ❌ Failed to send course_enrolled email for order {OrderId}", order.Id);
                }
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

                // Send group class enrollment email
                try
                {
                    var studentUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == order.BuyerUserId, cancellationToken);
                    var mentorUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == mentorUserId, cancellationToken);
                    var trCulture = new System.Globalization.CultureInfo("tr-TR");

                    _logger.LogInformation("📧 Group class enrollment: StudentEmail={Email}, Class={Class}",
                        studentUser?.Email ?? "NULL", enrollment.Class.Title);

                    if (studentUser?.Email != null)
                    {
                        await _emailService.SendTemplatedEmailAsync(
                            EmailTemplateKeys.GroupClassEnrolled,
                            studentUser.Email,
                            new Dictionary<string, string>
                            {
                                ["className"] = enrollment.Class.Title,
                                ["mentorName"] = mentorUser?.DisplayName ?? "Mentor",
                                ["classDate"] = enrollment.Class.StartAt.ToString("dd MMMM yyyy", trCulture),
                                ["classTime"] = enrollment.Class.StartAt.ToString("HH:mm")
                            },
                            cancellationToken);
                        _logger.LogInformation("📧 ✅ group_class_enrolled email sent to {Email}", studentUser.Email);
                    }
                    else
                    {
                        _logger.LogWarning("📧 Student email is null for order {OrderId}. Cannot send group class enrollment email.", order.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "📧 ❌ Failed to send group_class_enrolled email for order {OrderId}", order.Id);
                }
            }

            // Create ledger entries (escrow model) — for Booking, GroupClass, Course orders
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
