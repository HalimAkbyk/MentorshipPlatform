using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.MarkMessagesAsRead;

public record MarkMessagesAsReadCommand(
    Guid BookingId) : IRequest<Result>;

public class MarkMessagesAsReadCommandHandler : IRequestHandler<MarkMessagesAsReadCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IChatNotificationService _chatNotification;

    public MarkMessagesAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IChatNotificationService chatNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _chatNotification = chatNotification;
    }

    public async Task<Result> Handle(MarkMessagesAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return Result.Failure("Giriş yapmalısınız.");

        var userId = _currentUser.UserId!.Value;

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Rezervasyon bulunamadı.");

        if (booking.StudentUserId != userId && booking.MentorUserId != userId)
            return Result.Failure("Bu rezervasyona erişim yetkiniz yok.");

        // Mark unread messages from the other party as read
        var unreadMessages = await _context.Messages
            .Where(m => m.BookingId == request.BookingId
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
            await _chatNotification.NotifyMessagesRead(senderUserId.Value, request.BookingId, readMessageIds);
        }

        return Result.Success();
    }
}
