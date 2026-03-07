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
    string? AgendaItemsJson,
    string? PostSessionSummary,
    Guid? LinkedAssignmentId) : IRequest<Result>;

public class UpdateSessionPlanCommandValidator : AbstractValidator<UpdateSessionPlanCommand>
{
    public UpdateSessionPlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);
        RuleFor(x => x.PreSessionNote).MaximumLength(5000).When(x => x.PreSessionNote != null);
        RuleFor(x => x.SessionObjective).MaximumLength(5000).When(x => x.SessionObjective != null);
        RuleFor(x => x.SessionNotes).MaximumLength(10000).When(x => x.SessionNotes != null);
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

        if (plan.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only update your own session plans");

        plan.Update(
            request.Title,
            request.PreSessionNote,
            request.SessionObjective,
            request.SessionNotes,
            request.AgendaItemsJson,
            request.PostSessionSummary,
            request.LinkedAssignmentId);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
