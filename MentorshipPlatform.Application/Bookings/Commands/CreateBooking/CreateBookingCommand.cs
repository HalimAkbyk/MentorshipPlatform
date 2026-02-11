using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Bookings.Commands.CreateBooking;

public record CreateBookingCommand(
    Guid MentorUserId,
    Guid OfferingId,
    DateTime StartAt,
    int DurationMin,
    string? Notes) : IRequest<Result<Guid>>;

public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.StartAt)
            .GreaterThan(DateTime.UtcNow.AddHours(2))
            .WithMessage("Booking must be at least 2 hours in advance");

        RuleFor(x => x.DurationMin)
            .InclusiveBetween(30, 180)
            .WithMessage("Duration must be between 30 and 180 minutes");
    }
}

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IProcessHistoryService _history;

    public CreateBookingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IProcessHistoryService history)
    {
        _context = context;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<Result<Guid>> Handle(
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var studentUserId = _currentUser.UserId.Value;

        // Validate offering
        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId, cancellationToken);

        if (offering == null || !offering.IsActive)
            return Result<Guid>.Failure("Offering not found or inactive");

        // Check availability
        var endAt = request.StartAt.AddMinutes(request.DurationMin);
        var slotAvailable = await _context.AvailabilitySlots
            .AnyAsync(s =>
                s.MentorUserId == request.MentorUserId &&
                !s.IsBooked &&
                s.StartAt <= request.StartAt &&
                s.EndAt >= endAt,
                cancellationToken);

        if (!slotAvailable)
            return Result<Guid>.Failure("Selected time slot is not available");

        // Create booking
        var booking = Booking.Create(
            studentUserId,
            request.MentorUserId,
            request.OfferingId,
            request.StartAt,
            request.DurationMin);

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(cancellationToken);

        await _history.LogAsync("Booking", booking.Id, "Created",
            null, "PendingPayment",
            $"Booking oluşturuldu. Mentor: {request.MentorUserId}, Başlangıç: {request.StartAt:yyyy-MM-dd HH:mm}, Süre: {request.DurationMin}dk",
            studentUserId, "Student", ct: cancellationToken);

        return Result<Guid>.Success(booking.Id);
    }
}