using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetCourseAdminNotes;

public record CourseAdminNoteDto(
    Guid Id,
    Guid CourseId,
    Guid? LectureId,
    Guid AdminUserId,
    string AdminName,
    string NoteType,
    string? Flag,
    string Content,
    string? LectureTitle,
    DateTime CreatedAt);

public record GetCourseAdminNotesQuery(Guid CourseId) : IRequest<Result<List<CourseAdminNoteDto>>>;

public class GetCourseAdminNotesQueryHandler : IRequestHandler<GetCourseAdminNotesQuery, Result<List<CourseAdminNoteDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCourseAdminNotesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CourseAdminNoteDto>>> Handle(GetCourseAdminNotesQuery request, CancellationToken cancellationToken)
    {
        var courseExists = await _context.Courses
            .AnyAsync(c => c.Id == request.CourseId, cancellationToken);

        if (!courseExists)
            return Result<List<CourseAdminNoteDto>>.Failure("Course not found");

        var notes = await _context.CourseAdminNotes
            .Where(n => n.CourseId == request.CourseId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new CourseAdminNoteDto(
                n.Id,
                n.CourseId,
                n.LectureId,
                n.AdminUserId,
                n.AdminUser.DisplayName ?? "Admin",
                n.NoteType.ToString(),
                n.Flag.HasValue ? n.Flag.Value.ToString() : null,
                n.Content,
                n.LectureTitle,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<CourseAdminNoteDto>>.Success(notes);
    }
}
