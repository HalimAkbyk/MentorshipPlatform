using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.ShareSessionPlan;

public record ShareSessionPlanCommand(Guid Id) : IRequest<Result>;

public class ShareSessionPlanCommandHandler : IRequestHandler<ShareSessionPlanCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ShareSessionPlanCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ShareSessionPlanCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var plan = await _context.SessionPlans
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (plan == null)
            return Result.Failure("Session plan not found");

        if (plan.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only share your own session plans");

        plan.Share();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
