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

        // Kendi takvimine randevu alamaz
        if (studentUserId == request.MentorUserId)
            return Result<Guid>.Failure("Kendi takviminize randevu alamazsınız");

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

        // 1) Offering'e bağlı template'i resolve et
        Guid? resolvedTemplateId = offering.AvailabilityTemplateId;
        bool isDefaultTemplate = false;

        if (resolvedTemplateId.HasValue)
        {
            // Offering'e özel template var mı kontrol et
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
                .FirstOrDefaultAsync(t => t.MentorUserId == request.MentorUserId && t.IsDefault, cancellationToken);
            resolvedTemplateId = template?.Id;
            isDefaultTemplate = true;
        }
        else
        {
            template = await _context.AvailabilityTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == resolvedTemplateId.Value, cancellationToken);
        }

        // 2) Mentor'un bu zaman dilimini kapsayan müsait slot(lar) var mı? (template bazlı)
        var hasAvailableSlot = await _context.AvailabilitySlots
            .AnyAsync(s =>
                s.MentorUserId == request.MentorUserId &&
                !s.IsBooked &&
                s.StartAt <= bookingStart &&
                s.EndAt >= bookingEnd &&
                (s.TemplateId == resolvedTemplateId || (isDefaultTemplate && s.TemplateId == null)),
                cancellationToken);

        if (!hasAvailableSlot)
            return Result<Guid>.Failure("Seçilen zaman dilimi müsait değil");

        // 3) Buffer süresi al (resolved template'ten)
        var bufferMin = template?.BufferAfterMin ?? 15;

        // 4) Aynı kullanıcının bu mentor için ÖDENMEMİŞ (PendingPayment) booking'lerini otomatik iptal et
        //    Kullanıcı ödeme yapmadan geri dönüp tekrar booking oluşturursa, eski PendingPayment booking'ler
        //    yeni booking'i engellememelidir.
        var staleBookings = await _context.Bookings
            .Where(b =>
                b.StudentUserId == studentUserId &&
                b.MentorUserId == request.MentorUserId &&
                b.Status == BookingStatus.PendingPayment)
            .ToListAsync(cancellationToken);

        if (staleBookings.Any())
        {
            foreach (var stale in staleBookings)
            {
                stale.MarkAsExpired();
            }

            // İlişkili Pending order'ları abandoned olarak işaretle (ödeme yapılmadı)
            var staleBookingIds = staleBookings.Select(b => b.Id).ToList();
            var staleOrders = await _context.Orders
                .Where(o => staleBookingIds.Contains(o.ResourceId) && o.Status == OrderStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var staleOrder in staleOrders)
            {
                staleOrder.MarkAsAbandoned();
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        // 5) Aynı zaman diliminde başka bir AKTİF booking var mı? (buffer dahil — TÜM offering'ler arası çapraz kontrol)
        //    Buffer ders sonrası uygulanır:
        //    - Yeni ders, mevcut dersten SONRA başlıyorsa: bookingStart >= existingEnd + buffer
        //    - Yeni ders, mevcut dersten ÖNCE bitiyorsa: bookingEnd + buffer <= existingStart
        var hasConflictingBooking = await _context.Bookings
            .AnyAsync(b =>
                b.MentorUserId == request.MentorUserId &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment) &&
                bookingStart < b.EndAt.AddMinutes(bufferMin) &&
                bookingEnd.AddMinutes(bufferMin) > b.StartAt,
                cancellationToken);

        if (hasConflictingBooking)
            return Result<Guid>.Failure("Bu zaman diliminde zaten bir randevu mevcut (tampon süre dahil)");

        // Create booking
        var booking = Booking.Create(
            studentUserId,
            request.MentorUserId,
            request.OfferingId,
            bookingStart,
            request.DurationMin);

        _context.Bookings.Add(booking);

        // NOT: Slot'ları burada IsBooked olarak İŞARETLEMİYORUZ!
        // Slot kilitleme, ödeme başarılı olduktan sonra ProcessPaymentWebhookCommand'da yapılır.
        // Böylece ödeme başarısız olursa slot müsait kalır.
        // Çakışma kontrolü zaten yukarıda Bookings tablosu üzerinden yapılıyor (PendingPayment dahil).

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
