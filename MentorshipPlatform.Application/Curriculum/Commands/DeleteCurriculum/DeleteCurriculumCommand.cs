using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.DeleteCurriculum;

public record DeleteCurriculumCommand(Guid Id) : IRequest<Result>;

public class DeleteCurriculumCommandHandler : IRequestHandler<DeleteCurriculumCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteCurriculumCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCurriculumCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var curriculum = await _context.Curriculums
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (curriculum == null)
            return Result.Failure("Mufredat bulunamadi");

        if (curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizi silebilirsiniz");

        curriculum.Archive();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
