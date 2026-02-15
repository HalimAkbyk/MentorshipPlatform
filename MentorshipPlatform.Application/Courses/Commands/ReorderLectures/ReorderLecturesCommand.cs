using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.ReorderLectures;

public record ReorderLecturesCommand(Guid SectionId, List<Guid> LectureIds) : IRequest<Result>;

public class ReorderLecturesCommandValidator : AbstractValidator<ReorderLecturesCommand>
{
    public ReorderLecturesCommandValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.LectureIds).NotEmpty();
    }
}

public class ReorderLecturesCommandHandler : IRequestHandler<ReorderLecturesCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ReorderLecturesCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ReorderLecturesCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var section = await _context.CourseSections
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken);
        if (section == null) return Result.Failure("Section not found");
        if (section.Course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        var lectures = await _context.CourseLectures
            .Where(l => l.SectionId == request.SectionId)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < request.LectureIds.Count; i++)
        {
            var lecture = lectures.FirstOrDefault(l => l.Id == request.LectureIds[i]);
            lecture?.SetSortOrder(i);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
