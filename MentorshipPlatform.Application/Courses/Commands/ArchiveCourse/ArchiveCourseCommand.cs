using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.ArchiveCourse;

public record ArchiveCourseCommand(Guid CourseId) : IRequest<Result>;

public class ArchiveCourseCommandValidator : AbstractValidator<ArchiveCourseCommand>
{
    public ArchiveCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public class ArchiveCourseCommandHandler : IRequestHandler<ArchiveCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ArchiveCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ArchiveCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null) return Result.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        course.Archive();
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
