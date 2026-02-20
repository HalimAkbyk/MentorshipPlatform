using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.DisputeBooking;

public record DisputeBookingCommand(Guid BookingId, string Reason) : IRequest<Result>;

public class DisputeBookingCommandValidator : AbstractValidator<DisputeBookingCommand>
{
    public DisputeBookingCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000)
            .WithMessage("Dispute nedeni belirtilmeli (max 1000 karakter)");
    }
}

public class DisputeBookingCommandHandler : IRequestHandler<DisputeBookingCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;
    private readonly IAdminNotificationService _adminNotification;

    public DisputeBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history,
        IAdminNotificationService adminNotification)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
        _adminNotification = adminNotification;
    }

    public async Task<Result> Handle(DisputeBookingCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
            return Result.Failure("Booking not found");

        // Only the student can open a dispute
        if (booking.StudentUserId != userId)
            return Result.Failure("Yalnızca öğrenci dispute açabilir");

        try
        {
            var oldStatus = booking.Status.ToString();
            booking.Dispute(request.Reason);
            await _context.SaveChangesAsync(cancellationToken);

            await _history.LogAsync("Booking", booking.Id, "StatusChanged",
                oldStatus, "Disputed",
                $"Öğrenci dispute açtı: {request.Reason}",
                userId, "Student", ct: cancellationToken);

            // Admin notification
            await _adminNotification.CreateOrUpdateGroupedAsync(
                "Dispute",
                "pending-disputes",
                count => ("İtirazlar", $"Bekleyen {count} itiraz var"),
                "Dispute", booking.Id,
                cancellationToken);

            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
