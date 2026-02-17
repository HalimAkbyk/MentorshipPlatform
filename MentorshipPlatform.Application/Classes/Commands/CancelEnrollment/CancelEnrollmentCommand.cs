using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Classes.Commands.CancelEnrollment;

public record CancelEnrollmentCommand(Guid EnrollmentId, string Reason) : IRequest<Result<bool>>;

public class CancelEnrollmentCommandValidator : AbstractValidator<CancelEnrollmentCommand>
{
    public CancelEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class CancelEnrollmentCommandHandler
    : IRequestHandler<CancelEnrollmentCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _processHistory;

    public CancelEnrollmentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService processHistory)
    {
        _context = context;
        _currentUser = currentUser;
        _processHistory = processHistory;
    }

    public async Task<Result<bool>> Handle(
        CancelEnrollmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        var enrollment = await _context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken);

        if (enrollment == null)
            return Result<bool>.Failure("Enrollment not found");

        if (enrollment.StudentUserId != studentUserId)
            return Result<bool>.Failure("You can only cancel your own enrollments");

        if (enrollment.Status != EnrollmentStatus.Confirmed)
            return Result<bool>.Failure("Only confirmed enrollments can be cancelled");

        // Calculate refund percentage based on time
        var refundPercentage = enrollment.Class.CalculateRefundPercentage();

        // Cancel the enrollment
        enrollment.Cancel();

        // Create refund request if eligible
        if (refundPercentage > 0)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.ResourceId == enrollment.Id &&
                    o.Type == OrderType.GroupClass &&
                    (o.Status == OrderStatus.Paid || o.Status == OrderStatus.PartiallyRefunded),
                    cancellationToken);

            if (order != null)
            {
                var remainingRefundable = order.AmountTotal - order.RefundedAmount;
                var refundAmount = Math.Min(remainingRefundable, order.AmountTotal * refundPercentage);

                if (refundAmount > 0)
                {
                    var refundRequest = RefundRequest.Create(
                        order.Id,
                        studentUserId,
                        request.Reason,
                        refundAmount,
                        RefundType.StudentRequest);

                    _context.RefundRequests.Add(refundRequest);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _processHistory.LogAsync(
            "ClassEnrollment", enrollment.Id, "Cancelled",
            "Confirmed", "Cancelled",
            $"Ogrenci tarafindan iptal edildi. Iade orani: %{refundPercentage * 100:F0}",
            studentUserId, "Student",
            ct: cancellationToken);

        return Result<bool>.Success(true);
    }
}
