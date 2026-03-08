using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.UpdateSessionPlan;

public record UpdateSessionPlanCommand(
    Guid Id,
    string? Title,
    string? PreSessionNote,
    string? SessionObjective,
    string? SessionNotes,
    string? StudentNotes,
    string? AgendaItemsJson,
    string? PostSessionSummary,
    Guid? LinkedAssignmentId,
    Guid? BookingId = null,
    Guid? GroupClassId = null) : IRequest<Result>;

public class UpdateSessionPlanCommandValidator : AbstractValidator<UpdateSessionPlanCommand>
{
    public UpdateSessionPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);
        RuleFor(x => x.PreSessionNote).MaximumLength(5000).When(x => x.PreSessionNote != null);
        RuleFor(x => x.SessionObjective).MaximumLength(5000).When(x => x.SessionObjective != null);
        RuleFor(x => x.SessionNotes).MaximumLength(10000).When(x => x.SessionNotes != null);
        RuleFor(x => x.StudentNotes).MaximumLength(10000).When(x => x.StudentNotes != null);
        RuleFor(x => x.PostSessionSummary).MaximumLength(5000).When(x => x.PostSessionSummary != null);
    }
}

public class UpdateSessionPlanCommandHandler : IRequestHandler<UpdateSessionPlanCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateSessionPlanCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateSessionPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result.Failure("Session plan not found");

        var isMentor = plan.MentorUserId == _currentUser.UserId.Value;

        // Students can only update StudentNotes field
        if (!isMentor)
        {
            // Verify student has access to this plan via booking
            var hasAccess = false;
            if (plan.BookingId.HasValue)
            {
                hasAccess = await _context.Bookings
                    .AnyAsync(b => b.Id == plan.BookingId.Value && b.StudentUserId == _currentUser.UserId.Value, cancellationToken);
            }
            if (plan.GroupClassId.HasValue)
            {
                hasAccess = await _context.ClassEnrollments
                    .AnyAsync(e => e.ClassId == plan.GroupClassId.Value &&
                                   e.StudentUserId == _currentUser.UserId.Value &&
                                   e.Status == Domain.Enums.EnrollmentStatus.Confirmed, cancellationToken);
            }
            if (!hasAccess)
                return Result.Failure("You can only update your own session plans");

            // Student can only update StudentNotes
            if (request.StudentNotes != null) plan.UpdateStudentNotes(request.StudentNotes);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // Mentor: full update
        plan.Update(
            request.Title,
            request.PreSessionNote,
            request.SessionObjective,
            request.SessionNotes,
            request.AgendaItemsJson,
            request.PostSessionSummary,
            request.LinkedAssignmentId);

        if (request.StudentNotes != null) plan.UpdateStudentNotes(request.StudentNotes);

        // Link to booking/class if provided
        if (request.BookingId.HasValue) plan.LinkToBooking(request.BookingId.Value);
        if (request.GroupClassId.HasValue) plan.LinkToGroupClass(request.GroupClassId.Value);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
