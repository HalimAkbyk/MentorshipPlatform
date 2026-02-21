using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Classes.Commands.CancelEnrollment;

public record CancelEnrollmentCommand(Guid EnrollmentId, string Reason) : IRequest<Result<bool>>;

public class CancelEnrollmentCommandValidator : AbstractValidator<CancelEnrollmentCommand>
{
    public CancelEnrollmentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty().WithMessage("Kayıt ID gereklidir.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("İptal sebebi zorunludur.").MaximumLength(500).WithMessage("İptal sebebi en fazla 500 karakter olabilir.");
    }
}

public class CancelEnrollmentCommandHandler
    : IRequestHandler<CancelEnrollmentCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _processHistory;
    private readonly IEmailService _emailService;
    private readonly ILogger<CancelEnrollmentCommandHandler> _logger;

    public CancelEnrollmentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService processHistory,
        IEmailService emailService,
        ILogger<CancelEnrollmentCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _processHistory = processHistory;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        CancelEnrollmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("Oturum açmanız gerekiyor");

        var studentUserId = _currentUser.UserId.Value;

        var enrollment = await _context.ClassEnrollments
            .Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken);

        if (enrollment == null)
            return Result<bool>.Failure("Kayıt bulunamadı");

        if (enrollment.StudentUserId != studentUserId)
            return Result<bool>.Failure("Yalnızca kendi kayıtlarınızı iptal edebilirsiniz");

        if (enrollment.Status != EnrollmentStatus.Confirmed)
            return Result<bool>.Failure("Yalnızca onaylanmış kayıtlar iptal edilebilir");

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

        // Send cancellation confirmation to student
        try
        {
            var studentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == studentUserId, cancellationToken);
            var mentorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == enrollment.Class.MentorUserId, cancellationToken);

            if (studentUser?.Email != null)
            {
                await _emailService.SendTemplatedEmailAsync(
                    EmailTemplateKeys.GroupClassCancelled,
                    studentUser.Email,
                    new Dictionary<string, string>
                    {
                        ["className"] = enrollment.Class.Title,
                        ["mentorName"] = mentorUser?.DisplayName ?? "Mentor",
                        ["reason"] = $"{request.Reason} (İade oranı: %{refundPercentage * 100:F0})"
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send enrollment cancellation email for {EnrollmentId}", enrollment.Id);
        }

        return Result<bool>.Success(true);
    }
}
