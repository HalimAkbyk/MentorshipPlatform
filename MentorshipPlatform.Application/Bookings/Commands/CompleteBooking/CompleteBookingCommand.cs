using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.CompleteBooking;

public record CompleteBookingCommand(Guid BookingId) : IRequest<Result>;

public class CompleteBookingCommandHandler : IRequestHandler<CompleteBookingCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public CompleteBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result> Handle(CompleteBookingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        if (booking.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Unauthorized");

        // Seans süresi henüz dolmadıysa tamamlamaya izin verme
        if (DateTime.UtcNow < booking.EndAt)
        {
            await _history.LogAsync("Booking", booking.Id, "EarlyEndAttempt",
                booking.Status.ToString(), booking.Status.ToString(),
                $"Mentor seansı erken sonlandırmaya çalıştı. Planlanan bitiş: {booking.EndAt:HH:mm}, Şu an: {DateTime.UtcNow:HH:mm}",
                _currentUser.UserId.Value, "Mentor", ct: cancellationToken);

            return Result.Failure("Seans süresi henüz dolmadı. Video oturumu sonlandırıldı ancak seans devam ediyor. Odayı tekrar aktifleştirebilirsiniz.");
        }

        var oldStatus = booking.Status.ToString();
        booking.Complete();

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "StatusChanged",
            oldStatus, "Completed",
            "Mentor seansı tamamladı",
            _currentUser.UserId.Value, "Mentor", ct: cancellationToken);

        return Result.Success();
    }
}
