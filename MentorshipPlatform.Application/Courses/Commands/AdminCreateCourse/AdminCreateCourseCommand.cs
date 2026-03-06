using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.AdminCreateCourse;

public record AdminCreateCourseCommand(
    string Title,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string? Category,
    string? Language,
    string? Level,
    Guid? InstructorId) : IRequest<Result<Guid>>;

public class AdminCreateCourseCommandValidator : AbstractValidator<AdminCreateCourseCommand>
{
    public AdminCreateCourseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Kurs adi zorunludur").MaximumLength(200);
        RuleFor(x => x.ShortDescription).MaximumLength(500).When(x => x.ShortDescription != null);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Fiyat 0 veya daha fazla olmalidir");
        RuleFor(x => x.Category).MaximumLength(100).When(x => x.Category != null);
    }
}

public class AdminCreateCourseCommandHandler : IRequestHandler<AdminCreateCourseCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AdminCreateCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AdminCreateCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result<Guid>.Failure("Only admins can use this endpoint");

        var adminUserId = _currentUser.UserId.Value;

        // Validate instructor exists if provided
        if (request.InstructorId.HasValue)
        {
            var instructorExists = await _context.Users.AnyAsync(
                u => u.Id == request.InstructorId.Value, cancellationToken);
            if (!instructorExists)
                return Result<Guid>.Failure("Instructor not found");
        }

        var level = Enum.TryParse<CourseLevel>(request.Level, true, out var parsed) ? parsed : CourseLevel.AllLevels;

        var course = Course.Create(
            adminUserId,
            request.Title,
            request.Price,
            request.ShortDescription,
            request.Description,
            request.Category,
            request.Language,
            level);

        if (request.InstructorId.HasValue)
            course.SetInstructor(request.InstructorId.Value);

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(course.Id);
    }
}
