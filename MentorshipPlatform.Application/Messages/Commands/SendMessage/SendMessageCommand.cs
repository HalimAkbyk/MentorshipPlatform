using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Messages.Commands.SendMessage;

public record SendMessageCommand(
    Guid BookingId,
    string Content) : IRequest<Result<Guid>>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
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

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result<Guid>.Failure("Rezervasyon bulunamadı.");

        if (booking.StudentUserId != userId && booking.MentorUserId != userId)
            return Result<Guid>.Failure("Bu rezervasyona mesaj gönderme yetkiniz yok.");

        var message = Message.Create(request.BookingId, userId, request.Content);

        var recipientUserId = booking.StudentUserId == userId ? booking.MentorUserId : booking.StudentUserId;
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

        var payload = new
        {
            id = message.Id,
            bookingId = message.BookingId,
            senderUserId = userId,
            senderName = sender?.DisplayName ?? "Bilinmeyen",
            senderAvatar = sender?.AvatarUrl,
            content = message.Content,
            isRead = message.IsRead,
            isOwnMessage = false,
            createdAt = message.CreatedAt,
            deliveredAt = message.DeliveredAt,
            readAt = message.ReadAt
        };

        await _chatNotification.NotifyNewMessage(recipientUserId, payload);

        return Result<Guid>.Success(message.Id);
    }
}
