using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.FreeSessions.Commands.EndFreeSession;

public record EndFreeSessionCommand(Guid FreeSessionId) : IRequest<Result<bool>>;

public class EndFreeSessionCommandHandler : IRequestHandler<EndFreeSessionCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EndFreeSessionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(EndFreeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.FreeSessions
            .FirstOrDefaultAsync(s => s.Id == request.FreeSessionId, cancellationToken);

        if (session == null)
            return Result<bool>.Failure("Seans bulunamadi");

        if (session.MentorUserId != _currentUser.UserId)
            return Result<bool>.Failure("Bu seansi sadece olusturan egitmen bitirebilir");

        if (session.Status != FreeSessionStatus.Active && session.Status != FreeSessionStatus.Created)
            return Result<bool>.Failure("Bu seans zaten sona ermis");

        session.End();
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
