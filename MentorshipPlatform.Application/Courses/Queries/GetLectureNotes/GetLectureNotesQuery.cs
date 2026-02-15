using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetLectureNotes;

public record LectureNoteDto(Guid Id, int TimestampSec, string Content, DateTime CreatedAt);

public record GetLectureNotesQuery(Guid LectureId) : IRequest<Result<List<LectureNoteDto>>>;

public class GetLectureNotesQueryHandler : IRequestHandler<GetLectureNotesQuery, Result<List<LectureNoteDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetLectureNotesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<LectureNoteDto>>> Handle(GetLectureNotesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<List<LectureNoteDto>>.Failure("User not authenticated");

        var lecture = await _context.CourseLectures.Include(l => l.Section)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);
        if (lecture == null) return Result<List<LectureNoteDto>>.Failure("Lecture not found");

        var enrollment = await _context.CourseEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseId == lecture.Section.CourseId
                && e.StudentUserId == _currentUser.UserId.Value
                && e.Status == CourseEnrollmentStatus.Active, cancellationToken);
        if (enrollment == null) return Result<List<LectureNoteDto>>.Failure("Active enrollment not found");

        var notes = await _context.LectureNotes
            .AsNoTracking()
            .Where(n => n.EnrollmentId == enrollment.Id && n.LectureId == request.LectureId)
            .OrderBy(n => n.TimestampSec)
            .Select(n => new LectureNoteDto(n.Id, n.TimestampSec, n.Content, n.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<LectureNoteDto>>.Success(notes);
    }
}
