using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.SendMessage;

public record SendMessageCommand(
    Guid? ConversationId,
    Guid? BookingId,
    string Content) : IRequest<Result<Guid>>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => x.ConversationId.HasValue || x.BookingId.HasValue)
            .WithMessage("ConversationId veya BookingId gereklidir.");
    }
}

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IChatNotificationService _chatNotification;

    public SendMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _chatNotification = chatNotification;
    }

    public async Task<Result<Guid>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<Guid>.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;
        Conversation? conversation = null;
        Guid? bookingId = request.BookingId;

        if (request.ConversationId.HasValue)
        {
            // Send via conversation
            conversation = await _context.Conversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value, cancellationToken);

            if (conversation == null)
                return Result<Guid>.Failure("Konuşma bulunamadı.");

            if (!conversation.IsParticipant(userId))
                return Result<Guid>.Failure("Bu konuşmaya mesaj gönderme yetkiniz yok.");

            bookingId = conversation.BookingId;
        }
        else if (request.BookingId.HasValue)
        {
            // Legacy: send via booking — find or create conversation
            var booking = await _context.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.BookingId.Value, cancellationToken);

            if (booking == null)
                return Result<Guid>.Failure("Rezervasyon bulunamadı.");

            if (booking.StudentUserId != userId && booking.MentorUserId != userId)
                return Result<Guid>.Failure("Bu rezervasyona mesaj gönderme yetkiniz yok.");

            // Find or create conversation for this booking
            conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.BookingId == request.BookingId.Value, cancellationToken);

            if (conversation == null)
            {
                conversation = Conversation.CreateForBooking(
                    booking.StudentUserId, booking.MentorUserId, booking.Id);
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        if (conversation == null)
            return Result<Guid>.Failure("Konuşma bulunamadı.");

        var recipientUserId = conversation.GetOtherUserId(userId);
        var message = Message.Create(conversation.Id, bookingId, userId, request.Content);

        if (_chatNotification.IsUserOnline(recipientUserId))
        {
            message.MarkAsDelivered();
        }

        _context.Messages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        // Get sender info for notification payload
        var sender = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        var senderName = sender?.DisplayName ?? "Bilinmeyen";

        var payload = new
        {
            id = message.Id,
            conversationId = conversation.Id,
            bookingId = message.BookingId,
            senderUserId = userId,
            senderName,
            senderAvatar = sender?.AvatarUrl,
            content = message.Content,
            isRead = message.IsRead,
            isOwnMessage = false,
            createdAt = message.CreatedAt,
            deliveredAt = message.DeliveredAt,
            readAt = message.ReadAt
        };

        await _chatNotification.NotifyNewMessage(recipientUserId, payload);

        // Create a persistent notification for the recipient (bell icon)
        // Use groupKey to allow marking all message notifications from a conversation together
        var truncatedContent = message.Content.Length > 100
            ? message.Content[..100] + "…"
            : message.Content;
        var notification = UserNotification.Create(
            recipientUserId,
            "Message",
            $"{senderName} yeni bir mesaj gönderdi",
            truncatedContent,
            "Conversation",
            conversation.Id,
            $"msg-conv-{conversation.Id}");
        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify recipient about updated notification count via SignalR
        var unreadNotifCount = await _context.UserNotifications
            .CountAsync(n => n.UserId == recipientUserId && !n.IsRead, cancellationToken);
        await _chatNotification.NotifyNotificationCountUpdated(recipientUserId, unreadNotifCount);

        return Result<Guid>.Success(message.Id);
    }
}
