using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Commands.ApproveAccrual;

// Command
public record ApproveAccrualCommand(Guid AccrualId) : IRequest<Result>;

// Handler
public class ApproveAccrualCommandHandler : IRequestHandler<ApproveAccrualCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ApproveAccrualCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ApproveAccrualCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result.Failure("Bu işlem yalnızca admin tarafından yapılabilir.");

        if (!_currentUser.UserId.HasValue)
            return Result.Failure("Kullanıcı kimliği bulunamadı.");

        var accrual = await _context.InstructorAccruals
            .FirstOrDefaultAsync(a => a.Id == request.AccrualId, cancellationToken);

        if (accrual == null)
            return Result.Failure("Hakediş kaydı bulunamadı.");

        if (accrual.Status != AccrualStatus.Draft)
            return Result.Failure("Yalnızca taslak durumdaki hakedişler onaylanabilir.");

        accrual.Approve(_currentUser.UserId.Value);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
