using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Queries.GetMyConversations;

public record ConversationDto(
    Guid BookingId,
    Guid OtherUserId,
    string OtherUserName,
    string? OtherUserAvatar,
    string OfferingTitle,
    DateTime BookingStartAt,
    DateTime BookingEndAt,
    string BookingStatus,
    string? LastMessageContent,
    DateTime? LastMessageAt,
    bool LastMessageIsOwn,
    int UnreadCount);

public record GetMyConversationsQuery : IRequest<Result<List<ConversationDto>>>;

public class GetMyConversationsQueryHandler
    : IRequestHandler<GetMyConversationsQuery, Result<List<ConversationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyConversationsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ConversationDto>>> Handle(
        GetMyConversationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<List<ConversationDto>>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        // Get all bookings where user is participant AND has at least one message
        var bookingsWithMessages = await _context.Bookings
            .AsNoTracking()
            .Where(b => (b.StudentUserId == userId || b.MentorUserId == userId)
                        && _context.Messages.Any(m => m.BookingId == b.Id))
            .Select(b => new
            {
                b.Id,
                b.StudentUserId,
                b.MentorUserId,
                b.StartAt,
                b.EndAt,
                BookingStatus = b.Status.ToString(),
                OfferingTitle = b.Offering != null ? b.Offering.Title : "Bilinmeyen",
            })
            .ToListAsync(cancellationToken);

        if (!bookingsWithMessages.Any())
            return Result<List<ConversationDto>>.Success(new List<ConversationDto>());

        var bookingIds = bookingsWithMessages.Select(b => b.Id).ToList();

        // Get last message per booking
        var lastMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => bookingIds.Contains(m.BookingId))
            .GroupBy(m => m.BookingId)
            .Select(g => new
            {
                BookingId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).First()
            })
            .ToListAsync(cancellationToken);

        // Get unread counts per booking
        var unreadCounts = await _context.Messages
            .AsNoTracking()
            .Where(m => bookingIds.Contains(m.BookingId)
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .GroupBy(m => m.BookingId)
            .Select(g => new { BookingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BookingId, g => g.Count, cancellationToken);

        // Get other party user info
        var otherUserIds = bookingsWithMessages
            .Select(b => b.StudentUserId == userId ? b.MentorUserId : b.StudentUserId)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => otherUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl }, cancellationToken);

        var lastMessageDict = lastMessages.ToDictionary(m => m.BookingId);

        var conversations = bookingsWithMessages.Select(b =>
        {
            var otherUserId = b.StudentUserId == userId ? b.MentorUserId : b.StudentUserId;
            users.TryGetValue(otherUserId, out var otherUser);
            lastMessageDict.TryGetValue(b.Id, out var lastMsg);
            unreadCounts.TryGetValue(b.Id, out var unread);

            return new ConversationDto(
                b.Id,
                otherUserId,
                otherUser?.DisplayName ?? "Bilinmeyen",
                otherUser?.AvatarUrl,
                b.OfferingTitle,
                b.StartAt,
                b.EndAt,
                b.BookingStatus,
                lastMsg?.LastMessage.Content,
                lastMsg?.LastMessage.CreatedAt,
                lastMsg?.LastMessage.SenderUserId == userId,
                unread);
        })
        .OrderByDescending(c => c.LastMessageAt)
        .ToList();

        return Result<List<ConversationDto>>.Success(conversations);
    }
}
