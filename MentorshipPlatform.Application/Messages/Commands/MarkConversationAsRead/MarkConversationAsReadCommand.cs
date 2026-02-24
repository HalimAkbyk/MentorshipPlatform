using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.MarkConversationAsRead;

public record MarkConversationAsReadCommand(
    Guid ConversationId) : IRequest<Result>;

public class MarkConversationAsReadCommandHandler : IRequestHandler<MarkConversationAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IChatNotificationService _chatNotification;

    public MarkConversationAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _chatNotification = chatNotification;
    }

    public async Task<Result> Handle(MarkConversationAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);

        if (conversation == null)
            return Result.Failure("Konuşma bulunamadı.");

        if (!conversation.IsParticipant(userId))
            return Result.Failure("Bu konuşmaya erişim yetkiniz yok.");

        // Mark unread messages from the other party as read
        var unreadMessages = await _context.Messages
            .Where(m => m.ConversationId == request.ConversationId
                        && m.SenderUserId != userId
                        && !m.IsRead)
            .ToListAsync(cancellationToken);

        if (!unreadMessages.Any())
            return Result.Success();

        var readMessageIds = new List<Guid>();
        Guid? senderUserId = null;

        foreach (var message in unreadMessages)
        {
            message.MarkAsRead();
            readMessageIds.Add(message.Id);
            senderUserId ??= message.SenderUserId;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify the sender that their messages were read
        if (senderUserId != null && readMessageIds.Count > 0)
        {
            var bookingId = conversation.BookingId ?? Guid.Empty;
            await _chatNotification.NotifyMessagesRead(senderUserId.Value, bookingId, readMessageIds);
        }

        return Result.Success();
    }
}
