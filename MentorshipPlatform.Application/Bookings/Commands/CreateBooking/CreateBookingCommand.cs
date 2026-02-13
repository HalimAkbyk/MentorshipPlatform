using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.CreateBooking;

public record QuestionResponseDto(Guid QuestionId, string AnswerText);

public record CreateBookingCommand(
    Guid MentorUserId,
    Guid OfferingId,
    DateTime StartAt,
    int DurationMin,
    string? Notes,
    List<QuestionResponseDto>? QuestionResponses) : IRequest<Result<Guid>>;

public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.StartAt)
            .GreaterThan(DateTime.UtcNow.AddHours(1))
            .WithMessage("Randevu en az 1 saat önceden alınmalıdır");

        RuleFor(x => x.DurationMin)
            .InclusiveBetween(15, 180)
            .WithMessage("Süre 15 ile 180 dakika arasında olmalıdır");
    }
}

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public CreateBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result<Guid>> Handle(
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        // Validate offering (soruları da yükle)
        var offering = await _context.Offerings
            .Include(o => o.Questions)
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, cancellationToken);

        if (offering == null || !offering.IsActive)
            return Result<Guid>.Failure("Offering not found or inactive");

        // Offering'e ait MinNoticeHours kontrolü
        if (request.StartAt < DateTime.UtcNow.AddHours(offering.MinNoticeHours))
            return Result<Guid>.Failure($"Randevu en az {offering.MinNoticeHours} saat önceden alınmalıdır");

        // Offering'e ait MaxBookingDaysAhead kontrolü
        if (request.StartAt > DateTime.UtcNow.AddDays(offering.MaxBookingDaysAhead))
            return Result<Guid>.Failure($"En fazla {offering.MaxBookingDaysAhead} gün ilerisi için randevu alınabilir");

        // Required booking soruları cevap kontrolü
        var requiredQuestions = offering.Questions.Where(q => q.IsRequired).ToList();
        if (requiredQuestions.Any())
        {
            if (request.QuestionResponses == null || !request.QuestionResponses.Any())
                return Result<Guid>.Failure("Zorunlu sorular cevaplanmalıdır");

            var answeredIds = request.QuestionResponses.Select(r => r.QuestionId).ToHashSet();
            var unanswered = requiredQuestions.Where(q => !answeredIds.Contains(q.Id)).ToList();
            if (unanswered.Any())
                return Result<Guid>.Failure($"Zorunlu soru(lar) cevaplanmamış: {string.Join(", ", unanswered.Select(q => q.QuestionText))}");
        }

        // ===== KRİTİK: Slot çakışma kontrolü =====
        var bookingStart = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Utc);
        var bookingEnd = bookingStart.AddMinutes(request.DurationMin);

        // 1) Mentor'un bu zaman dilimini kapsayan müsait slot(lar) var mı?
        var hasAvailableSlot = await _context.AvailabilitySlots
            .AnyAsync(s =>
                s.MentorUserId == request.MentorUserId &&
                !s.IsBooked &&
                s.StartAt <= bookingStart &&
                s.EndAt >= bookingEnd,
                cancellationToken);

        if (!hasAvailableSlot)
            return Result<Guid>.Failure("Seçilen zaman dilimi müsait değil");

        // 2) Aynı zaman diliminde başka bir AKTİF booking var mı?
        //    Farklı paketlerden gelen çakışmaları da engeller!
        //    Örn: Paket A (60dk) cuma 11:00-12:00 alındıysa,
        //    Paket B (45dk) cuma 11:30-12:15 engellenecek.
        var hasConflictingBooking = await _context.Bookings
            .AnyAsync(b =>
                b.MentorUserId == request.MentorUserId &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment) &&
                b.StartAt < bookingEnd &&
                b.EndAt > bookingStart,
                cancellationToken);

        if (hasConflictingBooking)
            return Result<Guid>.Failure("Bu zaman diliminde zaten bir randevu mevcut");

        // Create booking
        var booking = Booking.Create(
            studentUserId,
            request.MentorUserId,
            request.OfferingId,
            bookingStart,
            request.DurationMin);

        _context.Bookings.Add(booking);

        // Slot'ları IsBooked olarak işaretle
        // Booking zaman aralığıyla çakışan TÜM slotları bul ve kilitle
        var overlappingSlots = await _context.AvailabilitySlots
            .Where(s =>
                s.MentorUserId == request.MentorUserId &&
                !s.IsBooked &&
                s.StartAt < bookingEnd &&
                s.EndAt > bookingStart)
            .ToListAsync(cancellationToken);

        foreach (var slot in overlappingSlots)
        {
            slot.MarkAsBooked();
        }

        // Booking sorularının cevaplarını kaydet
        if (request.QuestionResponses != null)
        {
            var validQuestionIds = offering.Questions.Select(q => q.Id).ToHashSet();
            foreach (var resp in request.QuestionResponses)
            {
                if (!validQuestionIds.Contains(resp.QuestionId))
                    continue;

                if (string.IsNullOrWhiteSpace(resp.AnswerText))
                    continue;

                var response = BookingQuestionResponse.Create(
                    booking.Id,
                    resp.QuestionId,
                    resp.AnswerText);
                _context.BookingQuestionResponses.Add(response);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "Created",
            null, "PendingPayment",
            $"Booking oluşturuldu. Mentor: {request.MentorUserId}, Paket: {offering.Title}, Başlangıç: {bookingStart:yyyy-MM-dd HH:mm}, Süre: {request.DurationMin}dk",
            studentUserId, "Student", ct: cancellationToken);

        return Result<Guid>.Success(booking.Id);
    }
}
