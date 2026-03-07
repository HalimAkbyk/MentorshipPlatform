using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Commands.RemoveSessionPlanMaterial;

public record RemoveSessionPlanMaterialCommand(Guid MaterialId) : IRequest<Result>;

public class RemoveSessionPlanMaterialCommandHandler : IRequestHandler<RemoveSessionPlanMaterialCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveSessionPlanMaterialCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(RemoveSessionPlanMaterialCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var material = await _context.SessionPlanMaterials
            .Include(x => x.SessionPlan)
            .FirstOrDefaultAsync(x => x.Id == request.MaterialId, cancellationToken);

        if (material == null)
            return Result.Failure("Session plan material not found");

        if (material.SessionPlan.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("You can only remove materials from your own session plans");

        _context.SessionPlanMaterials.Remove(material);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
