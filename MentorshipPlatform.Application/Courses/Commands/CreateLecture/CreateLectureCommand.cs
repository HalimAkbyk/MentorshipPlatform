using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.CreateLecture;

public record CreateLectureCommand(
    Guid SectionId,
    string Title,
    string? Type,
    bool IsPreview,
    string? Description) : IRequest<Result<Guid>>;

public class CreateLectureCommandValidator : AbstractValidator<CreateLectureCommand>
{
    public CreateLectureCommandValidator()
    {
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Ders adı zorunludur").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public class CreateLectureCommandHandler : IRequestHandler<CreateLectureCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateLectureCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateLectureCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<Guid>.Failure("User not authenticated");

        var section = await _context.CourseSections
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken);

        if (section == null) return Result<Guid>.Failure("Section not found");
        if (section.Course.MentorUserId != _currentUser.UserId.Value) return Result<Guid>.Failure("Not authorized");

        var maxSort = await _context.CourseLectures
            .Where(l => l.SectionId == request.SectionId)
            .MaxAsync(l => (int?)l.SortOrder, cancellationToken) ?? -1;

        var type = Enum.TryParse<LectureType>(request.Type, true, out var parsed) ? parsed : LectureType.Video;

        var lecture = CourseLecture.Create(request.SectionId, request.Title, type, maxSort + 1, request.IsPreview, request.Description);
        _context.CourseLectures.Add(lecture);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(lecture.Id);
    }
}
