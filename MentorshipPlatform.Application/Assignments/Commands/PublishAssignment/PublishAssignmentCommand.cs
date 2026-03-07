using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Commands.PublishAssignment;

public record PublishAssignmentCommand(Guid Id) : IRequest<Result<bool>>;

public class PublishAssignmentCommandHandler : IRequestHandler<PublishAssignmentCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PublishAssignmentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(PublishAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<bool>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (assignment == null)
            return Result<bool>.Failure("Odev bulunamadi");

        if (assignment.MentorUserId != _currentUser.UserId.Value)
            return Result<bool>.Failure("Bu odevi yayinlama yetkiniz yok");

        if (assignment.Status != AssignmentStatus.Draft)
            return Result<bool>.Failure("Sadece taslak odevler yayinlanabilir");

        assignment.Publish();
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
