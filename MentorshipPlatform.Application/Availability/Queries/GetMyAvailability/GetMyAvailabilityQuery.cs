using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Queries.GetMyAvailability;

public record MyAvailabilitySlotDto(Guid Id, DateTime StartAt, DateTime EndAt, bool IsBooked);

public record GetMyAvailabilityQuery(DateTime? From, DateTime? To)
    : IRequest<Result<List<MyAvailabilitySlotDto>>>;

public class GetMyAvailabilityQueryHandler
    : IRequestHandler<GetMyAvailabilityQuery, Result<List<MyAvailabilitySlotDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyAvailabilityQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<MyAvailabilitySlotDto>>> Handle(
        GetMyAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<MyAvailabilitySlotDto>>.Failure("User not authenticated");

        var from = request.From ?? DateTime.UtcNow;
        var to = request.To ?? from.AddDays(60);
        if (to < from) return Result<List<MyAvailabilitySlotDto>>.Failure("Invalid date range");

        var mentorUserId = _currentUser.UserId.Value;

        var slots = await _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.MentorUserId == mentorUserId)
            .Where(s => s.StartAt >= from && s.StartAt <= to)
            .OrderBy(s => s.StartAt)
            .Select(s => new MyAvailabilitySlotDto(s.Id, s.StartAt, s.EndAt, s.IsBooked))
            .ToListAsync(cancellationToken);

        return Result<List<MyAvailabilitySlotDto>>.Success(slots);
    }
}