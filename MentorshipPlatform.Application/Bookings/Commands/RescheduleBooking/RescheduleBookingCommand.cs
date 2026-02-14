using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.RescheduleBooking;

public record RescheduleBookingCommand(
    Guid BookingId,
    DateTime NewStartAt) : IRequest<Result>;

public class RescheduleBookingCommandValidator : AbstractValidator<RescheduleBookingCommand>
{
    public RescheduleBookingCommandValidator()
    {
        RuleFor(x => x.NewStartAt)
            .GreaterThan(DateTime.UtcNow.AddHours(2))
            .WithMessage("Yeni seans saati en az 2 saat sonrası olmalıdır");
    }
}

public class RescheduleBookingCommandHandler : IRequestHandler<RescheduleBookingCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public RescheduleBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result> Handle(RescheduleBookingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var booking = await _context.Bookings
            .Include(b => b.Offering)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        // Kullanici booking'e ait mi?
        bool isStudent = booking.StudentUserId == userId;
        bool isMentor = booking.MentorUserId == userId;
        if (!isStudent && !isMentor)
            return Result.Failure("Unauthorized");

        // Status kontrolu
        if (booking.Status != BookingStatus.Confirmed)
            return Result.Failure("Yalnizca onaylanmis seanslar yeniden planlanabilir");

        // Mevcut seanstan en az 2 saat once olmali
        if ((booking.StartAt - DateTime.UtcNow).TotalHours < 2)
            return Result.Failure("Seans saatine 2 saatten az kaldi, yeniden planlama yapilamaz");

        // Yeni saat en az 2 saat sonra olmali
        var newStartAt = DateTime.SpecifyKind(request.NewStartAt, DateTimeKind.Utc);
        if ((newStartAt - DateTime.UtcNow).TotalHours < 2)
            return Result.Failure("Yeni seans saati en az 2 saat sonrasi olmalidir");

        // ─── Slot & Conflict validasyonlari ───

        // 1) Offering'e bagli template'i resolve et
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

        var newEndAt = newStartAt.AddMinutes(booking.DurationMin);
        var bufferMin = template?.BufferAfterMin ?? 15;

        // 2) Yeni zaman dilimi icin musait slot var mi?
        var hasAvailableSlot = await _context.AvailabilitySlots
            .AnyAsync(s =>
                s.MentorUserId == booking.MentorUserId &&
                !s.IsBooked &&
                s.StartAt <= newStartAt &&
                s.EndAt >= newEndAt &&
                (s.TemplateId == resolvedTemplateId || (isDefaultTemplate && s.TemplateId == null)),
                cancellationToken);

        if (!hasAvailableSlot)
            return Result.Failure("Secilen zaman dilimi musait degil");

        // 3) Cakisma kontrolu (mevcut booking haric)
        var hasConflict = await _context.Bookings
            .AnyAsync(b =>
                b.Id != booking.Id &&
                b.MentorUserId == booking.MentorUserId &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment) &&
                newStartAt < b.EndAt.AddMinutes(bufferMin) &&
                newEndAt.AddMinutes(bufferMin) > b.StartAt,
                cancellationToken);

        if (hasConflict)
            return Result.Failure("Bu zaman diliminde zaten bir randevu mevcut (tampon sure dahil)");

        // ─── Islemi uygula ───
        var oldStartAt = booking.StartAt;

        if (isStudent)
        {
            // Student direkt reschedule yapar
            booking.Reschedule(newStartAt);

            await _context.SaveChangesAsync(cancellationToken);

            await _history.LogAsync("Booking", booking.Id, "Rescheduled",
                $"StartAt: {oldStartAt:yyyy-MM-dd HH:mm}", $"StartAt: {newStartAt:yyyy-MM-dd HH:mm}",
                $"Ogrenci seans saatini guncelledi. Eski: {oldStartAt:HH:mm}, Yeni: {newStartAt:HH:mm}",
                userId, "Student", ct: cancellationToken);

            return Result.Success();
        }
        else
        {
            // Mentor reschedule talebi olusturur — ogrenci onayina duser
            booking.RequestReschedule(newStartAt, userId);

            await _context.SaveChangesAsync(cancellationToken);

            await _history.LogAsync("Booking", booking.Id, "RescheduleRequested",
                null, null,
                $"Mentor seans saati degisikligi talep etti. Yeni: {newStartAt:yyyy-MM-dd HH:mm}",
                userId, "Mentor", ct: cancellationToken);

            return Result.Success();
        }
    }
}
