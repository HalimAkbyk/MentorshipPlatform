using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Refunds.Commands.InitiateRefund;

public record InitiateRefundCommand(
    Guid OrderId,
    decimal Amount,
    string Reason,
    bool IsGoodwill
) : IRequest<Result>;

public class InitiateRefundCommandValidator : AbstractValidator<InitiateRefundCommand>
{
    public InitiateRefundCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class InitiateRefundCommandHandler : IRequestHandler<InitiateRefundCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentService _paymentService;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<InitiateRefundCommandHandler> _logger;
    private readonly IPlatformSettingService _settings;
    private readonly IChatNotificationService _chatNotification;

    public InitiateRefundCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPaymentService paymentService,
        IProcessHistoryService processHistory,
        ILogger<InitiateRefundCommandHandler> logger,
        IPlatformSettingService settings,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _paymentService = paymentService;
        _processHistory = processHistory;
        _logger = logger;
        _settings = settings;
        _chatNotification = chatNotification;
    }

    public async Task<Result> Handle(InitiateRefundCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var adminId = _currentUser.UserId.Value;

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Result.Failure("Order not found");

        if (order.Status != OrderStatus.Paid && order.Status != OrderStatus.PartiallyRefunded)
            return Result.Failure("Only paid or partially refunded orders can be refunded");

        // Validate no over-refund
        var maxRefundable = order.AmountTotal - order.RefundedAmount;
        if (request.Amount > maxRefundable)
            return Result.Failure($"Refund amount ({request.Amount:F2}) exceeds maximum ({maxRefundable:F2})");

        // Call payment provider — Iyzico requires PaymentTransactionId (not PaymentId) for refunds
        var transactionId = order.ProviderTransactionId ?? order.ProviderPaymentId;
        if (string.IsNullOrEmpty(transactionId))
            return Result.Failure("Order has no payment transaction ID");

        var refundResult = await _paymentService.RefundPaymentAsync(
            transactionId, request.Amount, cancellationToken);

        if (!refundResult.Success)
        {
            _logger.LogError("Admin-initiated refund failed: {Error}", refundResult.ErrorMessage);
            return Result.Failure($"Payment provider refund failed: {refundResult.ErrorMessage}");
        }

        // Create refund request record
        var refundType = request.IsGoodwill ? RefundType.GoodwillCredit : RefundType.AdminInitiated;
        var refundRequest = RefundRequest.Create(
            order.Id, adminId, request.Reason, request.Amount, refundType);
        refundRequest.Approve(request.Amount, $"Admin-initiated ({refundType})", adminId);
        _context.RefundRequests.Add(refundRequest);

        // Create ledger entries
        if (request.IsGoodwill)
        {
            // Goodwill: only debit Platform, mentor keeps their money
            _context.LedgerEntries.Add(LedgerEntry.Create(
                LedgerAccountType.Platform, LedgerDirection.Debit,
                request.Amount, "GoodwillRefund", order.Id));
        }
        else
        {
            // Normal admin-initiated: proportional debit
            Guid? mentorUserId = null;
            if (order.Type == OrderType.Booking)
            {
                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == order.ResourceId, cancellationToken);
                mentorUserId = booking?.MentorUserId;

                if (booking != null && (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.Disputed))
                    booking.Cancel("Admin refund initiated");
            }
            else if (order.Type == OrderType.Course)
            {
                var enrollment = await _context.CourseEnrollments
                    .Include(e => e.Course)
                    .FirstOrDefaultAsync(e => e.Id == order.ResourceId, cancellationToken);
                mentorUserId = enrollment?.Course?.MentorUserId;
                enrollment?.Refund();
            }

            if (mentorUserId.HasValue)
            {
                var commissionRate = await _settings.GetDecimalAsync(
                    PlatformSettings.MentorCommissionRate, 0.15m, cancellationToken);
                var mentorPortion = request.Amount * (1 - commissionRate);
                var platformPortion = request.Amount * commissionRate;

                // Check where mentor funds are
                var netAvailable = await GetMentorNetAvailable(mentorUserId.Value, order.Id, cancellationToken);
                var accountToDebit = netAvailable > 0
                    ? LedgerAccountType.MentorAvailable
                    : LedgerAccountType.MentorEscrow;

                _context.LedgerEntries.Add(LedgerEntry.Create(
                    accountToDebit, LedgerDirection.Debit,
                    mentorPortion, "Refund", order.Id,
                    mentorUserId.Value));

                _context.LedgerEntries.Add(LedgerEntry.Create(
                    LedgerAccountType.Platform, LedgerDirection.Debit,
                    platformPortion, "Refund", order.Id));
            }
            else
            {
                // Fallback: full debit from platform
                _context.LedgerEntries.Add(LedgerEntry.Create(
                    LedgerAccountType.Platform, LedgerDirection.Debit,
                    request.Amount, "Refund", order.Id));
            }
        }

        // Student refund credit
        _context.LedgerEntries.Add(LedgerEntry.Create(
            LedgerAccountType.StudentRefund, LedgerDirection.Credit,
            request.Amount, "Refund", order.Id,
            order.BuyerUserId));

        // Update order
        order.MarkAsPartiallyRefunded(request.Amount);

        // Notify student about admin-initiated refund
        var studentNotif = UserNotification.Create(
            order.BuyerUserId,
            "RefundApproved",
            "İade Yapıldı",
            $"{request.Amount:F2} TL tutarında iade yapıldı. Ödeme yönteminize iade aktarılacaktır.",
            "Order", order.Id);
        _context.UserNotifications.Add(studentNotif);

        await _context.SaveChangesAsync(cancellationToken);

        // Push real-time notification count
        var unreadCount = await _context.UserNotifications
            .CountAsync(n => n.UserId == order.BuyerUserId && !n.IsRead, cancellationToken);
        await _chatNotification.NotifyNotificationCountUpdated(order.BuyerUserId, unreadCount);

        await _processHistory.LogAsync(
            "RefundRequest", refundRequest.Id, "AdminInitiated",
            null, "Approved",
            $"Admin initiated {refundType} refund of {request.Amount:F2} for order {order.Id}",
            adminId, "Admin",
            ct: cancellationToken);

        _logger.LogInformation("Admin-initiated refund: Type={Type}, Amount={Amount}, OrderId={OrderId}",
            refundType, request.Amount, order.Id);

        return Result.Success();
    }

    private async Task<decimal> GetMentorNetAvailable(Guid mentorUserId, Guid orderId, CancellationToken ct)
    {
        var credits = await _context.LedgerEntries
            .Where(l => l.AccountOwnerUserId == mentorUserId
                && l.AccountType == LedgerAccountType.MentorAvailable
                && l.ReferenceId == orderId
                && l.Direction == LedgerDirection.Credit)
            .SumAsync(l => l.Amount, ct);

        var debits = await _context.LedgerEntries
            .Where(l => l.AccountOwnerUserId == mentorUserId
                && l.AccountType == LedgerAccountType.MentorAvailable
                && l.ReferenceId == orderId
                && l.Direction == LedgerDirection.Debit)
            .SumAsync(l => l.Amount, ct);

        return credits - debits;
    }
}
