using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.CreateCourse;

public record CreateCourseCommand(
    string Title,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string? Category,
    string? Language,
    string? Level) : IRequest<Result<Guid>>;

public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Kurs adı zorunludur").MaximumLength(200);
        RuleFor(x => x.ShortDescription).MaximumLength(500).When(x => x.ShortDescription != null);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Fiyat 0 veya daha fazla olmalıdır");
        RuleFor(x => x.Category).MaximumLength(100).When(x => x.Category != null);
    }
}

public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<Guid>.Failure("User not authenticated");

        var mentorUserId = _currentUser.UserId.Value;
        var mentorExists = await _context.MentorProfiles.AnyAsync(m => m.UserId == mentorUserId, cancellationToken);
        if (!mentorExists)
            return Result<Guid>.Failure("Mentor profile not found");

        var level = Enum.TryParse<CourseLevel>(request.Level, true, out var parsed) ? parsed : CourseLevel.AllLevels;

        var course = Course.Create(
            mentorUserId,
            request.Title,
            request.Price,
            request.ShortDescription,
            request.Description,
            request.Category,
            request.Language,
            level);

        _context.Courses.Add(course);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(course.Id);
    }
}
