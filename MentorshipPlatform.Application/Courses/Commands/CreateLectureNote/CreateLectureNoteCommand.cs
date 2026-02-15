using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.CreateLectureNote;

public record CreateLectureNoteCommand(
    Guid LectureId,
    int TimestampSec,
    string Content) : IRequest<Result<Guid>>;

public class CreateLectureNoteCommandValidator : AbstractValidator<CreateLectureNoteCommand>
{
    public CreateLectureNoteCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
        RuleFor(x => x.TimestampSec).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public class CreateLectureNoteCommandHandler : IRequestHandler<CreateLectureNoteCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateLectureNoteCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateLectureNoteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<Guid>.Failure("User not authenticated");

        var lecture = await _context.CourseLectures.Include(l => l.Section)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);
        if (lecture == null) return Result<Guid>.Failure("Lecture not found");

        var enrollment = await _context.CourseEnrollments
            .FirstOrDefaultAsync(e => e.CourseId == lecture.Section.CourseId
                && e.StudentUserId == _currentUser.UserId.Value
                && e.Status == CourseEnrollmentStatus.Active, cancellationToken);
        if (enrollment == null) return Result<Guid>.Failure("Active enrollment not found");

        var note = LectureNote.Create(enrollment.Id, request.LectureId, request.TimestampSec, request.Content);
        _context.LectureNotes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(note.Id);
    }
}
