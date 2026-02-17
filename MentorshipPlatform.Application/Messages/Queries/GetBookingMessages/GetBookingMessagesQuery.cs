using MediatR;
using MentorshipPlatform.Application.Common.Extensions;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Queries.GetBookingMessages;

public record MessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderName,
    string? SenderAvatar,
    string Content,
    bool IsRead,
    bool IsOwnMessage,
    DateTime CreatedAt);

public record GetBookingMessagesQuery(
    Guid BookingId,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PaginatedList<MessageDto>>>;

public class GetBookingMessagesQueryHandler
    : IRequestHandler<GetBookingMessagesQuery, Result<PaginatedList<MessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetBookingMessagesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<MessageDto>>> Handle(
        GetBookingMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<PaginatedList<MessageDto>>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result<PaginatedList<MessageDto>>.Failure("Rezervasyon bulunamadı.");

        if (booking.StudentUserId != userId && booking.MentorUserId != userId)
            return Result<PaginatedList<MessageDto>>.Failure("Bu rezervasyonun mesajlarını görme yetkiniz yok.");

        var query = _context.Messages
            .AsNoTracking()
            .Where(m => m.BookingId == request.BookingId)
            .OrderBy(m => m.CreatedAt);

        var paginated = await query.ToPaginatedListAsync(request.Page, request.PageSize, cancellationToken);

        // Enrich with sender names
        var senderIds = paginated.Items.Select(m => m.SenderUserId).Distinct().ToList();
        var senders = await _context.Users
            .AsNoTracking()
            .Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl }, cancellationToken);

        var dtos = paginated.Items.Select(m =>
        {
            senders.TryGetValue(m.SenderUserId, out var sender);
            return new MessageDto(
                m.Id,
                m.SenderUserId,
                sender?.DisplayName ?? "Bilinmeyen",
                sender?.AvatarUrl,
                m.Content,
                m.IsRead,
                m.SenderUserId == userId,
                m.CreatedAt);
        }).ToList();

        return Result<PaginatedList<MessageDto>>.Success(
            new PaginatedList<MessageDto>(dtos, paginated.TotalCount, paginated.PageNumber, paginated.PageSize));
    }
}
