using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Commands.DeleteCurriculumWeek;

public record DeleteCurriculumWeekCommand(Guid WeekId) : IRequest<Result>;

public class DeleteCurriculumWeekCommandHandler : IRequestHandler<DeleteCurriculumWeekCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteCurriculumWeekCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteCurriculumWeekCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var week = await _context.CurriculumWeeks
            .Include(x => x.Curriculum)
            .Include(x => x.Topics)
            .FirstOrDefaultAsync(x => x.Id == request.WeekId, cancellationToken);

        if (week == null)
            return Result.Failure("Hafta bulunamadi");

        if (week.Curriculum.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Sadece kendi mufredatinizdaki haftalari silebilirsiniz");

        _context.CurriculumTopics.RemoveRange(week.Topics);
        _context.CurriculumWeeks.Remove(week);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
