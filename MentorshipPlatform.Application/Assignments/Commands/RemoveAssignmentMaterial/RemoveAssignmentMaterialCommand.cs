using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.RemoveAssignmentMaterial;

public record RemoveAssignmentMaterialCommand(Guid AssignmentId, Guid LibraryItemId) : IRequest<Result<bool>>;

public class RemoveAssignmentMaterialCommandHandler : IRequestHandler<RemoveAssignmentMaterialCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RemoveAssignmentMaterialCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(RemoveAssignmentMaterialCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId, cancellationToken);

        if (assignment == null)
            return Result<bool>.Failure("Odev bulunamadi");

        if (assignment.MentorUserId != _currentUser.UserId.Value)
            return Result<bool>.Failure("Bu odevden materyal kaldirma yetkiniz yok");

        var material = await _context.AssignmentMaterials
            .FirstOrDefaultAsync(x => x.AssignmentId == request.AssignmentId && x.LibraryItemId == request.LibraryItemId, cancellationToken);

        if (material == null)
            return Result<bool>.Failure("Materyal baglantisi bulunamadi");

        _context.AssignmentMaterials.Remove(material);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
