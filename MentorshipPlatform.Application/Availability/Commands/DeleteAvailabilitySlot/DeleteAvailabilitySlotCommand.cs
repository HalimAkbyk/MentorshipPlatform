using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Commands.DeleteAvailabilitySlot;

public record DeleteAvailabilitySlotCommand(Guid SlotId) : IRequest<Result<Unit>>;

public class DeleteAvailabilitySlotCommandHandler
    : IRequestHandler<DeleteAvailabilitySlotCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteAvailabilitySlotCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Unit>> Handle(DeleteAvailabilitySlotCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Unit>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var slot = await _context.AvailabilitySlots
            .FirstOrDefaultAsync(s => s.Id == request.SlotId, cancellationToken);

        if (slot == null)
            return Result<Unit>.Failure("Slot not found");

        if (slot.MentorUserId != userId)
            return Result<Unit>.Failure("Forbidden");

        if (slot.IsBooked)
            return Result<Unit>.Failure("Booked slot cannot be deleted");

        _context.AvailabilitySlots.Remove(slot);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}