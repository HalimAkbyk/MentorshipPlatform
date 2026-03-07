using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.PublishCurriculum;

public record PublishCurriculumCommand(Guid Id) : IRequest<Result>;

public class PublishCurriculumCommandHandler : IRequestHandler<PublishCurriculumCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PublishCurriculumCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(PublishCurriculumCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var curriculum = await _context.Curriculums
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (curriculum == null)
            return Result.Failure("Mufredat bulunamadi");

        if (curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizi yayinlayabilirsiniz");

        curriculum.Publish();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
