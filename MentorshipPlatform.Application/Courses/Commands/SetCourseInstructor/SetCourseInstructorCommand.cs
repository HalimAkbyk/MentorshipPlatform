using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.SetCourseInstructor;

public record SetCourseInstructorCommand(
    Guid CourseId,
    Guid? InstructorId) : IRequest<Result>;

public class SetCourseInstructorCommandValidator : AbstractValidator<SetCourseInstructorCommand>
{
    public SetCourseInstructorCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty().WithMessage("CourseId zorunludur");
    }
}

public class SetCourseInstructorCommandHandler : IRequestHandler<SetCourseInstructorCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SetCourseInstructorCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetCourseInstructorCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var course = await _context.Courses.FirstOrDefaultAsync(
            c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
            return Result.Failure("Course not found");

        // Only admin or course owner can set instructor
        var isAdmin = _currentUser.IsInRole(UserRole.Admin);
        var isOwner = course.MentorUserId == _currentUser.UserId.Value;
        if (!isAdmin && !isOwner)
            return Result.Failure("Not authorized");

        // Validate instructor exists if provided
        if (request.InstructorId.HasValue)
        {
            var instructorExists = await _context.Users.AnyAsync(
                u => u.Id == request.InstructorId.Value, cancellationToken);
            if (!instructorExists)
                return Result.Failure("Instructor not found");
        }

        course.SetInstructor(request.InstructorId);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
