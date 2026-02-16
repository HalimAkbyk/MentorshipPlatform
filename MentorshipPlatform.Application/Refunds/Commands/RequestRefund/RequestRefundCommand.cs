using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Refunds.Commands.RequestRefund;

public record RefundRequestDto(
    Guid Id,
    Guid OrderId,
    string Status,
    decimal RequestedAmount,
    string Reason,
    DateTime CreatedAt);

public record RequestRefundCommand(
    Guid OrderId,
    string Reason
) : IRequest<Result<RefundRequestDto>>;

public class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class RequestRefundCommandHandler
    : IRequestHandler<RequestRefundCommand, Result<RefundRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _processHistory;

    public RequestRefundCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService processHistory)
    {
        _context = context;
        _currentUser = currentUser;
        _processHistory = processHistory;
    }

    public async Task<Result<RefundRequestDto>> Handle(
        RequestRefundCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<RefundRequestDto>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // Find order and verify ownership
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Result<RefundRequestDto>.Failure("Order not found");

        if (order.BuyerUserId != userId)
            return Result<RefundRequestDto>.Failure("You can only request refunds for your own orders");

        // Check order is eligible for refund
        if (order.Status != OrderStatus.Paid && order.Status != OrderStatus.PartiallyRefunded)
            return Result<RefundRequestDto>.Failure("Only paid orders can be refunded");

        // Check no existing pending refund request
        var existingPending = await _context.RefundRequests
            .AnyAsync(r => r.OrderId == request.OrderId && r.Status == RefundRequestStatus.Pending, cancellationToken);

        if (existingPending)
            return Result<RefundRequestDto>.Failure("A refund request is already pending for this order");

        // Calculate eligible refund amount
        var remainingRefundable = order.AmountTotal - order.RefundedAmount;
        decimal eligibleAmount;

        if (order.Type == OrderType.Booking)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == order.ResourceId, cancellationToken);

            if (booking == null)
                return Result<RefundRequestDto>.Failure("Booking not found");

            var refundPercentage = booking.CalculateRefundPercentage();
            if (refundPercentage == 0)
                return Result<RefundRequestDto>.Failure(
                    "This booking is no longer eligible for a refund (less than 2 hours until start)");

            eligibleAmount = Math.Min(remainingRefundable, order.AmountTotal * refundPercentage);
        }
        else if (order.Type == OrderType.Course)
        {
            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.Id == order.ResourceId, cancellationToken);

            if (enrollment == null)
                return Result<RefundRequestDto>.Failure("Course enrollment not found");

            var refundPercentage = enrollment.CalculateCourseRefundPercentage();
            if (refundPercentage == 0)
            {
                return Result<RefundRequestDto>.Failure(
                    "This course is no longer eligible for a refund (refund window expired or too much progress)");
            }

            eligibleAmount = Math.Min(remainingRefundable, order.AmountTotal * refundPercentage);
        }
        else
        {
            // GroupClass — default: remaining refundable
            eligibleAmount = remainingRefundable;
        }

        if (eligibleAmount <= 0)
            return Result<RefundRequestDto>.Failure("No refundable amount remaining");

        // Create refund request
        var refundRequest = RefundRequest.Create(
            order.Id,
            userId,
            request.Reason,
            eligibleAmount,
            RefundType.StudentRequest);

        _context.RefundRequests.Add(refundRequest);
        await _context.SaveChangesAsync(cancellationToken);

        // Log
        await _processHistory.LogAsync(
            "RefundRequest", refundRequest.Id, "Created",
            null, "Pending",
            $"Student requested refund of {eligibleAmount:F2} for order {order.Id}",
            userId, "Student",
            ct: cancellationToken);

        return Result<RefundRequestDto>.Success(new RefundRequestDto(
            refundRequest.Id,
            refundRequest.OrderId,
            refundRequest.Status.ToString(),
            refundRequest.RequestedAmount,
            refundRequest.Reason,
            refundRequest.CreatedAt));
    }
}
