using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.UpdateLectureNote;

public record UpdateLectureNoteCommand(Guid NoteId, string Content, int? TimestampSec) : IRequest<Result>;

public class UpdateLectureNoteCommandValidator : AbstractValidator<UpdateLectureNoteCommand>
{
    public UpdateLectureNoteCommandValidator()
    {
        RuleFor(x => x.NoteId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public class UpdateLectureNoteCommandHandler : IRequestHandler<UpdateLectureNoteCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateLectureNoteCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateLectureNoteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var note = await _context.LectureNotes
            .Include(n => n.Enrollment)
            .FirstOrDefaultAsync(n => n.Id == request.NoteId, cancellationToken);
        if (note == null) return Result.Failure("Note not found");
        if (note.Enrollment.StudentUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        note.Update(request.Content, request.TimestampSec);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
