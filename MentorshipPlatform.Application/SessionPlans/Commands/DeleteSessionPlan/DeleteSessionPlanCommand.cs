using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.DeleteSessionPlan;

public record DeleteSessionPlanCommand(Guid Id) : IRequest<Result>;

public class DeleteSessionPlanCommandHandler : IRequestHandler<DeleteSessionPlanCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteSessionPlanCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteSessionPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .Include(x => x.Materials)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result.Failure("Session plan not found");

        if (plan.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only delete your own session plans");

        _context.SessionPlanMaterials.RemoveRange(plan.Materials);
        _context.SessionPlans.Remove(plan);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
