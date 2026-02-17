using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Queries.GetUnreadMessageCount;

public record BookingUnreadDto(Guid BookingId, int Count);

public record UnreadCountDto(int TotalUnread, List<BookingUnreadDto> PerBooking);

public record GetUnreadMessageCountQuery : IRequest<Result<UnreadCountDto>>;

public class GetUnreadMessageCountQueryHandler
    : IRequestHandler<GetUnreadMessageCountQuery, Result<UnreadCountDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadMessageCountQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<UnreadCountDto>> Handle(
        GetUnreadMessageCountQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<UnreadCountDto>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        // Find all bookings where user is participant
        var bookingIds = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.StudentUserId == userId || b.MentorUserId == userId)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        if (!bookingIds.Any())
            return Result<UnreadCountDto>.Success(new UnreadCountDto(0, new List<BookingUnreadDto>()));

        // Count unread messages per booking (messages not sent by current user and not read)
        var perBooking = await _context.Messages
            .AsNoTracking()
            .Where(m => bookingIds.Contains(m.BookingId)
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .GroupBy(m => m.BookingId)
            .Select(g => new BookingUnreadDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var total = perBooking.Sum(b => b.Count);

        return Result<UnreadCountDto>.Success(new UnreadCountDto(total, perBooking));
    }
}
