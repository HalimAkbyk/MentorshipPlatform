using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Queries.GetMentorAvailability;

public record AvailabilitySlotDto(Guid Id, DateTime StartAt, DateTime EndAt);

public record GetMentorAvailabilityQuery(
    Guid MentorUserId,
    DateTime? From,
    DateTime? To,
    bool IncludeBooked = false) : IRequest<Result<List<AvailabilitySlotDto>>>;

public class GetMentorAvailabilityQueryHandler
    : IRequestHandler<GetMentorAvailabilityQuery, Result<List<AvailabilitySlotDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetMentorAvailabilityQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<AvailabilitySlotDto>>> Handle(
        GetMentorAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var from = request.From ?? now;
        var to = request.To ?? from.AddDays(30);

        // Geçmiş slotları asla döndürme: from en az "şu an" olmalı
        if (from < now)
            from = now;

        if (to < from)
            return Result<List<AvailabilitySlotDto>>.Failure("Invalid date range");

        // Mentor var mı? (public endpoint için: mentor profili listeleniyor mu?)
        var mentorExists = await _context.MentorProfiles
            .AsNoTracking()
            .AnyAsync(m => m.UserId == request.MentorUserId && m.IsListed, cancellationToken);

        if (!mentorExists)
            return Result<List<AvailabilitySlotDto>>.Failure("Mentor not found");

        var q = _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.MentorUserId == request.MentorUserId)
            .Where(s => s.StartAt >= from && s.StartAt <= to);

        if (!request.IncludeBooked)
            q = q.Where(s => !s.IsBooked);

        var slots = await q
            .OrderBy(s => s.StartAt)
            .Select(s => new AvailabilitySlotDto(s.Id, s.StartAt, s.EndAt))
            .ToListAsync(cancellationToken);

        return Result<List<AvailabilitySlotDto>>.Success(slots);
    }
}