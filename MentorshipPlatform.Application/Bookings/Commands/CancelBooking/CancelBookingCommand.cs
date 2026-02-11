using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.CancelBooking;

public record CancelBookingCommand(Guid BookingId, string Reason) : IRequest<Result>;

public class CancelBookingCommandValidator : AbstractValidator<CancelBookingCommand>
{
    public CancelBookingCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class CancelBookingCommandHandler : IRequestHandler<CancelBookingCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public CancelBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result> Handle(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        if (booking.StudentUserId != userId && booking.MentorUserId != userId)
            return Result.Failure("Unauthorized");

        var role = booking.StudentUserId == userId ? "Student" : "Mentor";

        try
        {
            var oldStatus = booking.Status.ToString();
            booking.Cancel(request.Reason);

            // Release availability slot
            var slot = await _context.AvailabilitySlots
                .FirstOrDefaultAsync(s =>
                    s.MentorUserId == booking.MentorUserId &&
                    s.IsBooked &&
                    s.StartAt <= booking.StartAt &&
                    s.EndAt >= booking.EndAt,
                    cancellationToken);
            slot?.MarkAsAvailable();

            await _context.SaveChangesAsync(cancellationToken);

            await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                oldStatus, "Cancelled",
                $"Booking iptal edildi. Sebep: {request.Reason}",
                userId, role, ct: cancellationToken);

            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
