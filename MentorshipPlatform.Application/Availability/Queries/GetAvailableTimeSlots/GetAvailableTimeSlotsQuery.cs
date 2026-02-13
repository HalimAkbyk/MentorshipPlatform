using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Queries.GetAvailableTimeSlots;

public record TimeSlotDto(DateTime StartAt, DateTime EndAt, int DurationMin);

public record GetAvailableTimeSlotsQuery(
    Guid MentorUserId,
    Guid OfferingId,
    DateTime Date) : IRequest<Result<List<TimeSlotDto>>>;

public class GetAvailableTimeSlotsQueryHandler
    : IRequestHandler<GetAvailableTimeSlotsQuery, Result<List<TimeSlotDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAvailableTimeSlotsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TimeSlotDto>>> Handle(
        GetAvailableTimeSlotsQuery request,
        CancellationToken cancellationToken)
    {
        // 1) Offering bilgilerini al (duration)
        var offering = await _context.Offerings
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.IsActive, cancellationToken);

        if (offering == null)
            return Result<List<TimeSlotDto>>.Failure("Offering not found or inactive");

        var durationMin = offering.DurationMinDefault;

        // 2) Mentor template'inden buffer süresini al
        var template = await _context.AvailabilityTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.MentorUserId == request.MentorUserId && t.IsDefault, cancellationToken);

        var bufferMin = template?.BufferAfterMin ?? 15;
        var granularityMin = template?.SlotGranularityMin ?? 15;
        // Granularity en az 15 dk olmalı, sıfır veya negatif olmamalı
        if (granularityMin <= 0) granularityMin = 15;

        // 3) Timezone dönüşümü ile gün sınırlarını belirle
        var tz = FindTimezone(template?.Timezone ?? "Europe/Istanbul");
        var localDate = request.Date.Date;
        var localDayStart = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        var localDayEnd = DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified);
        var utcDayStart = TimeZoneInfo.ConvertTimeToUtc(localDayStart, tz);
        var utcDayEnd = TimeZoneInfo.ConvertTimeToUtc(localDayEnd, tz);

        // 4) Mentor'un bu gün için müsaitlik bloklarını al
        var availabilitySlots = await _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.MentorUserId == request.MentorUserId
                     && !s.IsBooked
                     && s.StartAt < utcDayEnd
                     && s.EndAt > utcDayStart)
            .OrderBy(s => s.StartAt)
            .ToListAsync(cancellationToken);

        if (!availabilitySlots.Any())
            return Result<List<TimeSlotDto>>.Success(new List<TimeSlotDto>());

        // 5) Mevcut aktif booking'leri al (buffer dahil kontrol için)
        var existingBookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.MentorUserId == request.MentorUserId
                     && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment)
                     && b.StartAt < utcDayEnd
                     && b.EndAt > utcDayStart)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.StartAt, b.EndAt })
            .ToListAsync(cancellationToken);

        // 6) Her müsaitlik bloğu içinde uygun time slot'ları hesapla
        //
        // Buffer mantığı: Bir ders bittikten sonra (endAt) + buffer kadar boşluk olmalı.
        // Yani mevcut booking.EndAt + bufferMin = bir sonraki dersin en erken başlayabileceği saat.
        // Ayrıca yeni bir ders konursa, bir sonraki ders de bu dersin EndAt + buffer'dan sonra başlamalı.
        // Buffer SONRA uygulanır (ders bitişinden sonra), ÖNCE uygulanmaz.
        //
        // Çakışma kontrolü:
        // Yeni slot [cursor, slotEnd] ile mevcut booking [booking.StartAt, booking.EndAt] arasında:
        // - Direkt çakışma: cursor < booking.EndAt && slotEnd > booking.StartAt
        // - Buffer çakışma: slotEnd'den sonra buffer olmalı, yani slotEnd + buffer > booking.StartAt ise
        //   ve cursor < booking.EndAt + buffer ise çakışır.
        // Basitleştirilmiş: cursor < booking.EndAt + buffer && slotEnd + buffer > booking.StartAt
        // Ama aslında buffer yalnız "sonra" uygulanır:
        //   - Yeni slot, mevcut booking'den SONRA başlıyorsa: cursor >= booking.EndAt + buffer olmalı
        //   - Yeni slot, mevcut booking'den ÖNCE bitiyorsa: slotEnd + buffer <= booking.StartAt olmalı
        //   - Aksi halde çakışma var

        var now = DateTime.UtcNow;
        var timeSlots = new List<TimeSlotDto>();

        foreach (var slot in availabilitySlots)
        {
            var windowStart = slot.StartAt;
            var windowEnd = slot.EndAt;

            // Geçmiş saatleri atla
            if (windowStart < now)
            {
                var minutesSinceStart = (now - windowStart).TotalMinutes;
                var granulesSkipped = (int)Math.Ceiling(minutesSinceStart / granularityMin);
                windowStart = slot.StartAt.AddMinutes(granulesSkipped * granularityMin);
            }

            // Granularity bazlı olası her başlangıç saatini kontrol et
            var cursor = windowStart;
            while (cursor.AddMinutes(durationMin) <= windowEnd)
            {
                var slotEnd = cursor.AddMinutes(durationMin);

                // Bu time slot mevcut booking'lerle çakışıyor mu?
                var hasConflict = false;
                foreach (var booking in existingBookings)
                {
                    // Yeni slot mevcut booking'den sonra mı başlıyor?
                    if (cursor >= booking.EndAt.AddMinutes(bufferMin))
                        continue; // Yeterince sonra, sorun yok

                    // Yeni slot mevcut booking'den önce mi bitiyor?
                    if (slotEnd.AddMinutes(bufferMin) <= booking.StartAt)
                        continue; // Yeterince önce, sorun yok

                    // Çakışma var
                    hasConflict = true;
                    break;
                }

                if (!hasConflict)
                {
                    timeSlots.Add(new TimeSlotDto(cursor, slotEnd, durationMin));
                }

                cursor = cursor.AddMinutes(granularityMin);
            }
        }

        return Result<List<TimeSlotDto>>.Success(timeSlots);
    }

    private static TimeZoneInfo FindTimezone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Europe/Istanbul"] = "Turkey Standard Time",
                ["Turkey Standard Time"] = "Europe/Istanbul",
            };
            if (mapping.TryGetValue(timezone, out var alt))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(alt); }
                catch { /* fall through */ }
            }
            return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "Turkey", "Turkey Standard Time");
        }
    }
}
