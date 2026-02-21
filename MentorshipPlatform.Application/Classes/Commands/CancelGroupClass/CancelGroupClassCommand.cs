using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Classes.Commands.CancelGroupClass;

public record CancelGroupClassCommand(Guid ClassId, string? Reason) : IRequest<Result<bool>>;

public class CancelGroupClassCommandValidator : AbstractValidator<CancelGroupClassCommand>
{
    public CancelGroupClassCommandValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty().WithMessage("Ders ID gereklidir.");
    }
}

public class CancelGroupClassCommandHandler
    : IRequestHandler<CancelGroupClassCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _processHistory;
    private readonly IEmailService _emailService;
    private readonly ILogger<CancelGroupClassCommandHandler> _logger;

    public CancelGroupClassCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService processHistory,
        IEmailService emailService,
        ILogger<CancelGroupClassCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _processHistory = processHistory;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        CancelGroupClassCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("Oturum açmanız gerekiyor");

        var mentorUserId = _currentUser.UserId.Value;

        var groupClass = await _context.GroupClasses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == request.ClassId, cancellationToken);

        if (groupClass == null)
            return Result<bool>.Failure("Grup dersi bulunamadı");

        if (groupClass.MentorUserId != mentorUserId)
            return Result<bool>.Failure("Yalnızca kendi derslerinizi iptal edebilirsiniz");

        if (groupClass.Status == ClassStatus.Cancelled || groupClass.Status == ClassStatus.Completed)
            return Result<bool>.Failure("Bu ders iptal edilemez");

        // Cancel the class
        groupClass.Cancel();

        // Cancel all confirmed enrollments and initiate refunds
        var confirmedEnrollments = groupClass.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Confirmed)
            .ToList();

        foreach (var enrollment in confirmedEnrollments)
        {
            enrollment.Cancel();

            // Find the order for this enrollment and create auto-refund request
            var order = await _context.Orders
                .FirstOrDefaultAsync(o =>
                    o.ResourceId == enrollment.Id &&
                    o.Type == OrderType.GroupClass &&
                    (o.Status == OrderStatus.Paid || o.Status == OrderStatus.PartiallyRefunded),
                    cancellationToken);

            if (order != null)
            {
                var refundAmount = order.AmountTotal - order.RefundedAmount;
                if (refundAmount > 0)
                {
                    var refundRequest = RefundRequest.Create(
                        order.Id,
                        mentorUserId,
                        request.Reason ?? "Mentor tarafindan grup dersi iptal edildi",
                        refundAmount,
                        RefundType.AdminInitiated);

                    _context.RefundRequests.Add(refundRequest);
                }
            }
        }

        // Cancel pending enrollments too
        var pendingEnrollments = groupClass.Enrollments
            .Where(e => e.Status == EnrollmentStatus.PendingPayment)
            .ToList();

        foreach (var enrollment in pendingEnrollments)
        {
            enrollment.Cancel();
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _processHistory.LogAsync(
            "GroupClass", groupClass.Id, "Cancelled",
            "Published", "Cancelled",
            $"Mentor tarafindan iptal edildi. {confirmedEnrollments.Count} kayit icin iade talebi olusturuldu.",
            mentorUserId, "Mentor",
            ct: cancellationToken);

        // Send cancellation emails to all enrolled students
        try
        {
            var mentorUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == mentorUserId, cancellationToken);

            var studentIds = confirmedEnrollments.Select(e => e.StudentUserId).Distinct().ToList();
            var students = await _context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id))
                .ToListAsync(cancellationToken);

            foreach (var student in students)
            {
                if (string.IsNullOrEmpty(student.Email)) continue;
                try
                {
                    await _emailService.SendTemplatedEmailAsync(
                        EmailTemplateKeys.GroupClassCancelled,
                        student.Email,
                        new Dictionary<string, string>
                        {
                            ["className"] = groupClass.Title,
                            ["mentorName"] = mentorUser?.DisplayName ?? "Mentor",
                            ["reason"] = request.Reason ?? "Belirtilmedi"
                        },
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send group class cancellation email to {Email}", student.Email);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send group class cancellation emails for {ClassId}", groupClass.Id);
        }

        return Result<bool>.Success(true);
    }
}
