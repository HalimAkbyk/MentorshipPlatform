using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Refunds.Commands.ProcessRefund;

public record ProcessRefundCommand(
    Guid RefundRequestId,
    bool IsApproved,
    decimal? OverrideAmount,
    string? AdminNotes
) : IRequest<Result>;

public class ProcessRefundCommandValidator : AbstractValidator<ProcessRefundCommand>
{
    public ProcessRefundCommandValidator()
    {
        RuleFor(x => x.RefundRequestId).NotEmpty();
        RuleFor(x => x.AdminNotes).MaximumLength(1000);
        RuleFor(x => x.OverrideAmount)
            .GreaterThan(0).When(x => x.OverrideAmount.HasValue)
            .WithMessage("Override amount must be greater than 0");
    }
}

public class ProcessRefundCommandHandler : IRequestHandler<ProcessRefundCommand, Result>
{
    private const decimal PLATFORM_COMMISSION_RATE = 0.15m;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentService _paymentService;
    private readonly IProcessHistoryService _processHistory;
    private readonly ILogger<ProcessRefundCommandHandler> _logger;

    public ProcessRefundCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPaymentService paymentService,
        IProcessHistoryService processHistory,
        ILogger<ProcessRefundCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _paymentService = paymentService;
        _processHistory = processHistory;
        _logger = logger;
    }

    public async Task<Result> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var adminId = _currentUser.UserId.Value;

        var refundRequest = await _context.RefundRequests
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == request.RefundRequestId, cancellationToken);

        if (refundRequest == null)
            return Result.Failure("Refund request not found");

        if (refundRequest.Status != RefundRequestStatus.Pending)
            return Result.Failure("Refund request is not pending");

        var order = refundRequest.Order;

        // Reject
        if (!request.IsApproved)
        {
            refundRequest.Reject(request.AdminNotes, adminId);
            await _context.SaveChangesAsync(cancellationToken);

            await _processHistory.LogAsync(
                "RefundRequest", refundRequest.Id, "Rejected",
                "Pending", "Rejected",
                $"Admin rejected refund request. Notes: {request.AdminNotes}",
                adminId, "Admin",
                ct: cancellationToken);

            return Result.Success();
        }

        // Approve
        var refundAmount = request.OverrideAmount ?? refundRequest.RequestedAmount;

        // Validate: no over-refund
        var maxRefundable = order.AmountTotal - order.RefundedAmount;
        if (refundAmount > maxRefundable)
            return Result.Failure($"Refund amount ({refundAmount:F2}) exceeds maximum refundable ({maxRefundable:F2})");

        // Call payment provider
        if (string.IsNullOrEmpty(order.ProviderPaymentId))
            return Result.Failure("Order has no payment provider ID — cannot process refund");

        var refundResult = await _paymentService.RefundPaymentAsync(
            order.ProviderPaymentId, refundAmount, cancellationToken);

        if (!refundResult.Success)
        {
            _logger.LogError("Payment provider refund failed for order {OrderId}: {Error}",
                order.Id, refundResult.ErrorMessage);
            return Result.Failure($"Payment provider refund failed: {refundResult.ErrorMessage}");
        }

        // Determine mentor userId
        Guid? mentorUserId = null;
        if (order.Type == OrderType.Booking)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == order.ResourceId, cancellationToken);
            mentorUserId = booking?.MentorUserId;

            // Cancel booking if still active
            if (booking != null && (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.Disputed))
            {
                booking.Cancel("Refund approved");
            }
        }
        else if (order.Type == OrderType.Course)
        {
            var enrollment = await _context.CourseEnrollments
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == order.ResourceId, cancellationToken);

            if (enrollment != null)
            {
                mentorUserId = enrollment.Course?.MentorUserId;
                enrollment.Refund();
            }
        }

        // Create refund ledger entries (coupon-aware reverse split)
        if (mentorUserId.HasValue)
        {
            decimal mentorRefundPortion, platformRefundPortion;
            bool isAdminCoupon = order.DiscountAmount > 0
                && string.Equals(order.CouponCreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase);

            if (isAdminCoupon)
            {
                // Admin coupon: mentor was credited based on original price, platform took the hit.
                // On refund, reverse the same proportions.
                var originalPrice = order.AmountTotal + order.DiscountAmount;
                var originalMentorNet = originalPrice * (1 - PLATFORM_COMMISSION_RATE);
                var originalPlatformCommission = order.AmountTotal - originalMentorNet;

                // Refund ratio (partial refund support)
                var refundRatio = refundAmount / order.AmountTotal;
                mentorRefundPortion = originalMentorNet * refundRatio;
                platformRefundPortion = originalPlatformCommission * refundRatio;
            }
            else
            {
                // Standard split (mentor coupon or no coupon)
                mentorRefundPortion = refundAmount * (1 - PLATFORM_COMMISSION_RATE);
                platformRefundPortion = refundAmount * PLATFORM_COMMISSION_RATE;
            }

            // Determine if mentor funds are in escrow or available
            var mentorAvailableCredits = await _context.LedgerEntries
                .Where(l => l.AccountOwnerUserId == mentorUserId.Value
                    && l.AccountType == LedgerAccountType.MentorAvailable
                    && l.ReferenceId == order.Id
                    && l.Direction == LedgerDirection.Credit)
                .SumAsync(l => l.Amount, cancellationToken);

            var mentorAvailableDebits = await _context.LedgerEntries
                .Where(l => l.AccountOwnerUserId == mentorUserId.Value
                    && l.AccountType == LedgerAccountType.MentorAvailable
                    && l.ReferenceId == order.Id
                    && l.Direction == LedgerDirection.Debit)
                .SumAsync(l => l.Amount, cancellationToken);

            var netAvailable = mentorAvailableCredits - mentorAvailableDebits;

            // Debit from MentorAvailable if funds were released, else MentorEscrow
            var mentorAccountToDebit = netAvailable > 0
                ? LedgerAccountType.MentorAvailable
                : LedgerAccountType.MentorEscrow;

            _context.LedgerEntries.Add(LedgerEntry.Create(
                mentorAccountToDebit, LedgerDirection.Debit,
                mentorRefundPortion, "Refund", order.Id,
                mentorUserId.Value));

            _context.LedgerEntries.Add(LedgerEntry.Create(
                LedgerAccountType.Platform, LedgerDirection.Debit,
                platformRefundPortion, "Refund", order.Id));

            _context.LedgerEntries.Add(LedgerEntry.Create(
                LedgerAccountType.StudentRefund, LedgerDirection.Credit,
                refundAmount, "Refund", order.Id,
                order.BuyerUserId));
        }

        // Update order
        order.MarkAsPartiallyRefunded(refundAmount);

        // Approve refund request
        refundRequest.Approve(refundAmount, request.AdminNotes, adminId);

        await _context.SaveChangesAsync(cancellationToken);

        await _processHistory.LogAsync(
            "RefundRequest", refundRequest.Id, "Approved",
            "Pending", "Approved",
            $"Admin approved refund of {refundAmount:F2}. Override: {request.OverrideAmount?.ToString("F2") ?? "none"}",
            adminId, "Admin",
            ct: cancellationToken);

        _logger.LogInformation("Refund approved: RequestId={RequestId}, Amount={Amount}, OrderId={OrderId}",
            refundRequest.Id, refundAmount, order.Id);

        return Result.Success();
    }
}
