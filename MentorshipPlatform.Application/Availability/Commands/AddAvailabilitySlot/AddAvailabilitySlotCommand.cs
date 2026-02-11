using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Availability.Commands.AddAvailabilitySlot;

public record AddAvailabilitySlotCommand(
    DateTime StartAt,
    DateTime EndAt) : IRequest<Result<Guid>>;

public class AddAvailabilitySlotCommandValidator : AbstractValidator<AddAvailabilitySlotCommand>
{
    public AddAvailabilitySlotCommandValidator()
    {
        RuleFor(x => x.StartAt)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Start time must be in the future");

        RuleFor(x => x.EndAt)
            .GreaterThan(x => x.StartAt)
            .WithMessage("End time must be after start time");

        RuleFor(x => x)
            .Must(x => (x.EndAt - x.StartAt).TotalMinutes >= 30)
            .WithMessage("Slot must be at least 30 minutes");
    }
}

public class AddAvailabilitySlotCommandHandler 
    : IRequestHandler<AddAvailabilitySlotCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AddAvailabilitySlotCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(
        AddAvailabilitySlotCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;

        // Check for overlapping slots
        var hasOverlap = await _context.AvailabilitySlots
            .AnyAsync(s =>
                s.MentorUserId == mentorUserId &&
                !s.IsBooked &&
                ((s.StartAt <= request.StartAt && s.EndAt > request.StartAt) ||
                 (s.StartAt < request.EndAt && s.EndAt >= request.EndAt) ||
                 (s.StartAt >= request.StartAt && s.EndAt <= request.EndAt)),
                cancellationToken);

        if (hasOverlap)
            return Result<Guid>.Failure("This time slot overlaps with an existing slot");

        var slot = AvailabilitySlot.Create(mentorUserId, request.StartAt, request.EndAt);
        _context.AvailabilitySlots.Add(slot);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(slot.Id);
    }
}