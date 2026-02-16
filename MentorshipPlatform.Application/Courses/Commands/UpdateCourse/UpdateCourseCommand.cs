using System.Text.Json;
using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.UpdateCourse;

public record UpdateCourseCommand(
    Guid CourseId,
    string Title,
    string? ShortDescription,
    string? Description,
    decimal Price,
    string? Category,
    string? Language,
    string? Level,
    string? CoverImageUrl,
    string? CoverImagePosition,
    string? CoverImageTransform,
    string? PromoVideoKey,
    List<string>? WhatYouWillLearn,
    List<string>? Requirements,
    List<string>? TargetAudience) : IRequest<Result>;

public class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortDescription).MaximumLength(500).When(x => x.ShortDescription != null);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public class UpdateCourseCommandHandler : IRequestHandler<UpdateCourseCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateCourseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateCourseCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result.Failure("User not authenticated");

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course == null)
            return Result.Failure("Course not found");
        if (course.MentorUserId != _currentUser.UserId.Value)
            return Result.Failure("Not authorized");

        var level = Enum.TryParse<CourseLevel>(request.Level, true, out var parsed) ? parsed : course.Level;

        course.Update(
            request.Title,
            request.ShortDescription,
            request.Description,
            request.Price,
            request.Category,
            request.Language,
            level,
            request.CoverImageUrl,
            request.CoverImagePosition,
            request.CoverImageTransform,
            request.PromoVideoKey,
            request.WhatYouWillLearn != null ? JsonSerializer.Serialize(request.WhatYouWillLearn) : null,
            request.Requirements != null ? JsonSerializer.Serialize(request.Requirements) : null,
            request.TargetAudience != null ? JsonSerializer.Serialize(request.TargetAudience) : null);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
