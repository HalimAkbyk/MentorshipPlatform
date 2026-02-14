using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.RejectReschedule;

public record RejectRescheduleCommand(Guid BookingId) : IRequest<Result>;

public class RejectRescheduleCommandHandler : IRequestHandler<RejectRescheduleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public RejectRescheduleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result> Handle(RejectRescheduleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        // Yalnizca student reddedebilir
        if (booking.StudentUserId != userId)
            return Result.Failure("Yalnizca ogrenci reschedule talebini reddedebilir");

        if (!booking.PendingRescheduleStartAt.HasValue)
            return Result.Failure("Reddedilecek bir reschedule talebi yok");

        var pendingStartAt = booking.PendingRescheduleStartAt.Value;
        booking.RejectReschedule();

        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "RescheduleRejected",
            null, null,
            $"Ogrenci mentor'un reschedule talebini reddetti. Talep edilen saat: {pendingStartAt:yyyy-MM-dd HH:mm}",
            userId, "Student", ct: cancellationToken);

        return Result.Success();
    }
}
