using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Offerings.Commands.ToggleOffering;

public record ToggleOfferingCommand(Guid OfferingId) : IRequest<Result<bool>>;

public class ToggleOfferingCommandHandler : IRequestHandler<ToggleOfferingCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ToggleOfferingCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(ToggleOfferingCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var offering = await _context.Offerings
            .FirstOrDefaultAsync(o => o.Id == request.OfferingId && o.MentorUserId == _currentUser.UserId.Value, ct);

        if (offering == null)
            return Result<bool>.Failure("Offering not found");

        if (offering.IsActive)
            offering.Deactivate();
        else
            offering.Activate();

        await _context.SaveChangesAsync(ct);
        return Result<bool>.Success(offering.IsActive);
    }
}
