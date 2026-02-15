using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.PublishCourse;

public record PublishCourseCommand(Guid CourseId) : IRequest<Result>;

public class PublishCourseCommandValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public class PublishCourseCommandHandler : IRequestHandler<PublishCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PublishCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(PublishCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var course = await _context.Courses
            .Include(c => c.Sections)
                .ThenInclude(s => s.Lectures)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null) return Result.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        if (!course.Sections.Any() || !course.Sections.Any(s => s.Lectures.Any()))
            return Result.Failure("Kurs yayınlanmadan önce en az bir bölüm ve bir ders eklenmelidir");

        if (course.Price <= 0)
            return Result.Failure("Kurs fiyatı belirlenmeden yayınlanamaz");

        // Recalculate stats
        var totalLectures = course.Sections.Sum(s => s.Lectures.Count);
        var totalDuration = course.Sections.Sum(s => s.Lectures.Sum(l => l.DurationSec));
        course.UpdateStats(totalDuration, totalLectures);

        course.Publish();
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
