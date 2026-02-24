using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Queries.GetMyConversations;

public record ConversationDto(
    Guid ConversationId,
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
    int UnreadCount,
    string ConversationType);

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

        // Get all conversations where user is a participant
        var conversations = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .Select(c => new
            {
                c.Id,
                c.User1Id,
                c.User2Id,
                c.BookingId,
                ConvType = c.Type.ToString(),
            })
            .ToListAsync(cancellationToken);

        if (!conversations.Any())
        {
            // Fallback: check for legacy booking-based messages without conversations
            return await HandleLegacyBookingMessages(userId, cancellationToken);
        }

        var conversationIds = conversations.Select(c => c.Id).ToList();

        // Get last message per conversation
        var lastMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue && conversationIds.Contains(m.ConversationId.Value))
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).First()
            })
            .ToListAsync(cancellationToken);

        // Filter to conversations with messages
        var activeConversationIds = lastMessages.Select(lm => lm.ConversationId).ToHashSet();
        var activeConversations = conversations.Where(c => activeConversationIds.Contains(c.Id)).ToList();

        if (!activeConversations.Any())
        {
            return await HandleLegacyBookingMessages(userId, cancellationToken);
        }

        // Get unread counts per conversation
        var unreadCounts = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue && conversationIds.Contains(m.ConversationId.Value)
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ConversationId, g => g.Count, cancellationToken);

        // Get booking info for booking-based conversations
        var bookingIds = activeConversations
            .Where(c => c.BookingId.HasValue)
            .Select(c => c.BookingId!.Value)
            .Distinct()
            .ToList();

        var bookings = bookingIds.Any()
            ? await _context.Bookings
                .AsNoTracking()
                .Where(b => bookingIds.Contains(b.Id))
                .Select(b => new
                {
                    b.Id,
                    b.StartAt,
                    b.EndAt,
                    BookingStatus = b.Status.ToString(),
                    OfferingTitle = b.Offering != null ? b.Offering.Title : "Bilinmeyen",
                })
                .ToDictionaryAsync(b => b.Id, cancellationToken)
            : new();

        // Get other party user info
        var otherUserIds = activeConversations
            .Select(c => c.User1Id == userId ? c.User2Id : c.User1Id)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => otherUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.AvatarUrl }, cancellationToken);

        var lastMessageDict = lastMessages.ToDictionary(m => m.ConversationId);

        var result = activeConversations.Select(c =>
        {
            var otherUserId = c.User1Id == userId ? c.User2Id : c.User1Id;
            users.TryGetValue(otherUserId, out var otherUser);
            lastMessageDict.TryGetValue(c.Id, out var lastMsg);
            unreadCounts.TryGetValue(c.Id, out var unread);

            string offeringTitle = "Direkt Mesaj";
            DateTime bookingStartAt = default;
            DateTime bookingEndAt = default;
            string bookingStatus = "";
            Guid bookingIdForDto = Guid.Empty;

            if (c.BookingId.HasValue && bookings.TryGetValue(c.BookingId.Value, out var booking))
            {
                offeringTitle = booking.OfferingTitle;
                bookingStartAt = booking.StartAt;
                bookingEndAt = booking.EndAt;
                bookingStatus = booking.BookingStatus;
                bookingIdForDto = c.BookingId.Value;
            }

            return new ConversationDto(
                c.Id,
                bookingIdForDto,
                otherUserId,
                otherUser?.DisplayName ?? "Bilinmeyen",
                otherUser?.AvatarUrl,
                offeringTitle,
                bookingStartAt,
                bookingEndAt,
                bookingStatus,
                lastMsg?.LastMessage.Content,
                lastMsg?.LastMessage.CreatedAt,
                lastMsg?.LastMessage.SenderUserId == userId,
                unread,
                c.ConvType);
        })
        .OrderByDescending(c => c.LastMessageAt)
        .ToList();

        return Result<List<ConversationDto>>.Success(result);
    }

    /// <summary>
    /// Fallback for legacy messages that were created before the Conversation entity.
    /// Returns booking-based conversations from direct message queries.
    /// </summary>
    private async Task<Result<List<ConversationDto>>> HandleLegacyBookingMessages(
        Guid userId, CancellationToken cancellationToken)
    {
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

        var lastMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.BookingId.HasValue && bookingIds.Contains(m.BookingId.Value))
            .GroupBy(m => m.BookingId!.Value)
            .Select(g => new
            {
                BookingId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).First()
            })
            .ToListAsync(cancellationToken);

        var unreadCounts = await _context.Messages
            .AsNoTracking()
            .Where(m => m.BookingId.HasValue && bookingIds.Contains(m.BookingId.Value)
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .GroupBy(m => m.BookingId!.Value)
            .Select(g => new { BookingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BookingId, g => g.Count, cancellationToken);

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
                Guid.Empty, // no conversation entity yet
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
                unread,
                "Booking");
        })
        .OrderByDescending(c => c.LastMessageAt)
        .ToList();

        return Result<List<ConversationDto>>.Success(conversations);
    }
}
