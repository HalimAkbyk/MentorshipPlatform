using MediatR;
using MentorshipPlatform.Application.Common.Constants;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Bookings.Commands.ApproveReschedule;

public record ApproveRescheduleCommand(Guid BookingId) : IRequest<Result>;

public class ApproveRescheduleCommandHandler : IRequestHandler<ApproveRescheduleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IEmailService _emailService;
    private readonly ILogger<ApproveRescheduleCommandHandler> _logger;

    public ApproveRescheduleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IEmailService emailService,
        ILogger<ApproveRescheduleCommandHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result> Handle(ApproveRescheduleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var booking = await _context.Bookings
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        // Yalnizca student onaylayabilir
        if (booking.StudentUserId != userId)
            return Result.Failure("Yalnizca ogrenci reschedule talebini onaylayabilir");

        if (!booking.PendingRescheduleStartAt.HasValue)
            return Result.Failure("Onay bekleyen bir reschedule talebi yok");

        // Slot conflict tekrar kontrol et (aradan gecen zamanda baskasi almis olabilir)
        var newStartAt = booking.PendingRescheduleStartAt.Value;
        var newEndAt = booking.PendingRescheduleEndAt!.Value;

        // Template resolve
        Guid? resolvedTemplateId = booking.Offering.AvailabilityTemplateId;
        bool isDefaultTemplate = false;

        if (resolvedTemplateId.HasValue)
        {
            var exists = await _context.AvailabilityTemplates
                .AsNoTracking()
                .AnyAsync(t => t.Id == resolvedTemplateId.Value, cancellationToken);
            if (!exists) resolvedTemplateId = null;
        }

        Domain.Entities.AvailabilityTemplate? template = null;
        if (!resolvedTemplateId.HasValue)
        {
            template = await _context.AvailabilityTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.MentorUserId == booking.MentorUserId && t.IsDefault, cancellationToken);
            resolvedTemplateId = template?.Id;
            isDefaultTemplate = true;
        }
        else
        {
            template = await _context.AvailabilityTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == resolvedTemplateId.Value, cancellationToken);
        }

        var bufferMin = template?.BufferAfterMin ?? 15;

        // Cakisma kontrolu (mevcut booking haric)
        var hasConflict = await _context.Bookings
            .AnyAsync(b =>
                b.Id != booking.Id &&
                b.MentorUserId == booking.MentorUserId &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment) &&
                newStartAt < b.EndAt.AddMinutes(bufferMin) &&
                newEndAt.AddMinutes(bufferMin) > b.StartAt,
                cancellationToken);

        if (hasConflict)
            return Result.Failure("Bu zaman dilimi artik musait degil. Mentor yeni bir saat secmelidir.");

        var oldStartAt = booking.StartAt;
        booking.ApproveReschedule();

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "RescheduleApproved",
            $"StartAt: {oldStartAt:yyyy-MM-dd HH:mm}", $"StartAt: {newStartAt:yyyy-MM-dd HH:mm}",
            $"Ogrenci mentor'un reschedule talebini onayladi. Eski: {oldStartAt:HH:mm}, Yeni: {newStartAt:HH:mm}",
            userId, "Student", ct: cancellationToken);

        // Notify mentor that reschedule was approved
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                EmailTemplateKeys.RescheduleApproved,
                booking.Mentor.Email!,
                new Dictionary<string, string>
                {
                    ["otherPartyName"] = booking.Student.DisplayName,
                    ["newDate"] = newStartAt.ToString("dd MMMM yyyy HH:mm", trCulture),
                    ["offeringTitle"] = booking.Offering?.Title ?? "Seans"
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send reschedule approved email for {BookingId}", booking.Id);
        }

        return Result.Success();
    }
}
