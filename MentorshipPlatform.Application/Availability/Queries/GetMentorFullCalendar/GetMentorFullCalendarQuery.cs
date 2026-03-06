using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Queries.GetMentorFullCalendar;

public enum CalendarSlotType
{
    Available,
    Booked,
    Unavailable
}

public record CalendarSlotDto(DateTime StartAt, DateTime EndAt, string Type);

public record GetMentorFullCalendarQuery(
    Guid MentorUserId,
    Guid OfferingId,
    DateTime Start,
    DateTime End) : IRequest<Result<List<CalendarSlotDto>>>;

public class GetMentorFullCalendarQueryHandler
    : IRequestHandler<GetMentorFullCalendarQuery, Result<List<CalendarSlotDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMentorFullCalendarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CalendarSlotDto>>> Handle(
        GetMentorFullCalendarQuery request,
        CancellationToken cancellationToken)
    {
        var offering = await _context.Offerings
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.IsActive, cancellationToken);

        if (offering == null)
            return Result<List<CalendarSlotDto>>.Failure("Paket bulunamadi");

        var durationMin = offering.DurationMinDefault;
        var start = request.Start.ToUniversalTime();
        var end = request.End.ToUniversalTime();

        // Get available slots
        var availableSlots = await _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.MentorUserId == request.MentorUserId
                     && !s.IsBooked
                     && s.StartAt < end
                     && s.EndAt > start)
            .OrderBy(s => s.StartAt)
            .ToListAsync(cancellationToken);

        // Get existing bookings (confirmed, pending)
        var bookedSlots = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.MentorUserId == request.MentorUserId
                     && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.PendingPayment)
                     && b.StartAt < end
                     && b.EndAt > start)
            .Select(b => new { b.StartAt, b.EndAt })
            .OrderBy(b => b.StartAt)
            .ToListAsync(cancellationToken);

        var result = new List<CalendarSlotDto>();

        // Mark available slots
        foreach (var slot in availableSlots)
        {
            // Generate sub-slots based on duration
            var cursor = slot.StartAt < start ? start : slot.StartAt;
            var slotEnd = slot.EndAt > end ? end : slot.EndAt;

            while (cursor.AddMinutes(durationMin) <= slotEnd)
            {
                var subEnd = cursor.AddMinutes(durationMin);

                // Check if this sub-slot overlaps with any booking
                var isBooked = bookedSlots.Any(b =>
                    cursor < b.EndAt && subEnd > b.StartAt);

                result.Add(new CalendarSlotDto(
                    cursor,
                    subEnd,
                    isBooked ? CalendarSlotType.Booked.ToString() : CalendarSlotType.Available.ToString()));

                cursor = cursor.AddMinutes(durationMin);
            }
        }

        // Generate unavailable slots for gaps (hourly blocks for the time range)
        // Only generate for business hours (8:00 - 22:00 local time)
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var currentDay = start.Date;
        while (currentDay < end)
        {
            for (var hour = 8; hour < 22; hour++)
            {
                var localStart = new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, hour, 0, 0, DateTimeKind.Unspecified);
                var utcSlotStart = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
                var utcSlotEnd = utcSlotStart.AddMinutes(durationMin);

                if (utcSlotStart < start || utcSlotEnd > end) continue;
                if (utcSlotStart < DateTime.UtcNow) continue;

                // Skip if already covered
                var alreadyCovered = result.Any(r =>
                    utcSlotStart >= r.StartAt && utcSlotStart < r.EndAt);

                if (!alreadyCovered)
                {
                    result.Add(new CalendarSlotDto(
                        utcSlotStart,
                        utcSlotEnd,
                        CalendarSlotType.Unavailable.ToString()));
                }
            }
            currentDay = currentDay.AddDays(1);
        }

        return Result<List<CalendarSlotDto>>.Success(
            result.OrderBy(r => r.StartAt).ToList());
    }
}
