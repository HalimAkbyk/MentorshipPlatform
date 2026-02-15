using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.UpdateLecture;

public record UpdateLectureCommand(
    Guid LectureId,
    string Title,
    string? Description,
    bool IsPreview,
    string? TextContent) : IRequest<Result>;

public class UpdateLectureCommandValidator : AbstractValidator<UpdateLectureCommand>
{
    public UpdateLectureCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public class UpdateLectureCommandHandler : IRequestHandler<UpdateLectureCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateLectureCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateLectureCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var lecture = await _context.CourseLectures
            .Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);

        if (lecture == null) return Result.Failure("Lecture not found");
        if (lecture.Section.Course.MentorUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        lecture.Update(request.Title, request.Description, request.IsPreview, request.TextContent);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
