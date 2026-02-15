using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.DeleteLectureNote;

public record DeleteLectureNoteCommand(Guid NoteId) : IRequest<Result>;

public class DeleteLectureNoteCommandValidator : AbstractValidator<DeleteLectureNoteCommand>
{
    public DeleteLectureNoteCommandValidator()
    {
        RuleFor(x => x.NoteId).NotEmpty();
    }
}

public class DeleteLectureNoteCommandHandler : IRequestHandler<DeleteLectureNoteCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteLectureNoteCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteLectureNoteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result.Failure("User not authenticated");

        var note = await _context.LectureNotes
            .Include(n => n.Enrollment)
            .FirstOrDefaultAsync(n => n.Id == request.NoteId, cancellationToken);
        if (note == null) return Result.Failure("Note not found");
        if (note.Enrollment.StudentUserId != _currentUser.UserId.Value) return Result.Failure("Not authorized");

        _context.LectureNotes.Remove(note);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
