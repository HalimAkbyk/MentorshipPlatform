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

        // Count unread messages across all conversations where user is a participant
        var conversationIds = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // Also include legacy booking-based messages
        var bookingIds = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.StudentUserId == userId || b.MentorUserId == userId)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        if (!conversationIds.Any() && !bookingIds.Any())
            return Result<UnreadCountDto>.Success(new UnreadCountDto(0, new List<BookingUnreadDto>()));

        // Count unread: via conversation or via legacy booking
        var perBooking = await _context.Messages
            .AsNoTracking()
            .Where(m => (m.ConversationId.HasValue && conversationIds.Contains(m.ConversationId.Value) ||
                        (m.BookingId.HasValue && bookingIds.Contains(m.BookingId.Value)))
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .Where(m => m.BookingId.HasValue)
            .GroupBy(m => m.BookingId!.Value)
            .Select(g => new BookingUnreadDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        // Also count direct messages with no booking
        var directUnread = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue && conversationIds.Contains(m.ConversationId.Value)
                        && !m.BookingId.HasValue
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .CountAsync(cancellationToken);

        var total = perBooking.Sum(b => b.Count) + directUnread;

        return Result<UnreadCountDto>.Success(new UnreadCountDto(total, perBooking));
    }
}
