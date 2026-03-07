using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.CompleteSessionPlan;

public record CompleteSessionPlanCommand(Guid Id) : IRequest<Result>;

public class CompleteSessionPlanCommandHandler : IRequestHandler<CompleteSessionPlanCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CompleteSessionPlanCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(CompleteSessionPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result.Failure("Session plan not found");

        if (plan.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only complete your own session plans");

        plan.Complete();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
