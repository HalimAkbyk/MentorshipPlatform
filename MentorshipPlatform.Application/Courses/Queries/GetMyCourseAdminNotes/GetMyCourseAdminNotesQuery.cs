using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetMyCourseAdminNotes;

public record MyCourseAdminNoteDto(
    Guid Id, string NoteType, string? Flag, string Content,
    Guid? LectureId, string? LectureTitle, DateTime CreatedAt);

public record GetMyCourseAdminNotesQuery(Guid CourseId) : IRequest<Result<List<MyCourseAdminNoteDto>>>;

public class GetMyCourseAdminNotesQueryHandler : IRequestHandler<GetMyCourseAdminNotesQuery, Result<List<MyCourseAdminNoteDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCourseAdminNotesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<MyCourseAdminNoteDto>>> Handle(GetMyCourseAdminNotesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<MyCourseAdminNoteDto>>.Failure("User not authenticated");

        // Verify course ownership
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
            return Result<List<MyCourseAdminNoteDto>>.Failure("Kurs bulunamadı");

        if (course.MentorUserId != _currentUser.UserId.Value)
            return Result<List<MyCourseAdminNoteDto>>.Failure("Bu kursa erişim yetkiniz yok");

        var notes = await _context.CourseAdminNotes
            .AsNoTracking()
            .Where(n => n.CourseId == request.CourseId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new MyCourseAdminNoteDto(
                n.Id,
                n.NoteType.ToString(),
                n.Flag.HasValue ? n.Flag.Value.ToString() : null,
                n.Content,
                n.LectureId,
                n.LectureTitle,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<MyCourseAdminNoteDto>>.Success(notes);
    }
}
