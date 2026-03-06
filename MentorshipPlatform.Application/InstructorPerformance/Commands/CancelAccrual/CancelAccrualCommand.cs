using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Commands.CancelAccrual;

// Command
public record CancelAccrualCommand(Guid AccrualId, string? Notes) : IRequest<Result>;

// Handler
public class CancelAccrualCommandHandler : IRequestHandler<CancelAccrualCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CancelAccrualCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(CancelAccrualCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result.Failure("Bu işlem yalnızca admin tarafından yapılabilir.");

        var accrual = await _context.InstructorAccruals
            .FirstOrDefaultAsync(a => a.Id == request.AccrualId, cancellationToken);

        if (accrual == null)
            return Result.Failure("Hakediş kaydı bulunamadı.");

        if (accrual.Status == AccrualStatus.Paid)
            return Result.Failure("Ödenmiş hakedişler iptal edilemez.");

        if (accrual.Status == AccrualStatus.Cancelled)
            return Result.Failure("Bu hakediş zaten iptal edilmiş.");

        accrual.Cancel();

        if (!string.IsNullOrWhiteSpace(request.Notes))
            accrual.AddNote(request.Notes);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
