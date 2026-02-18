using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetCoursePlayer;

public record PlayerLectureDto(Guid Id, string Title, int DurationSec, bool IsPreview, string Type, bool IsCompleted, bool IsActive);
public record PlayerSectionDto(Guid Id, string Title, List<PlayerLectureDto> Lectures);

public record CoursePlayerDto(
    string CourseTitle,
    Guid CurrentLectureId,
    string CurrentLectureTitle,
    string? CurrentLectureDescription,
    string CurrentLectureType,
    string? VideoUrl,
    string? TextContent,
    int LastPositionSec,
    decimal CompletionPercentage,
    List<PlayerSectionDto> Sections);

public record GetCoursePlayerQuery(Guid CourseId, Guid? LectureId) : IRequest<Result<CoursePlayerDto>>;

public class GetCoursePlayerQueryHandler : IRequestHandler<GetCoursePlayerQuery, Result<CoursePlayerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;

    public GetCoursePlayerQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IStorageService storage)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<Result<CoursePlayerDto>> Handle(GetCoursePlayerQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<CoursePlayerDto>.Failure("User not authenticated");
        var studentId = _currentUser.UserId.Value;

        var course = await _context.Courses
            .AsNoTracking()
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lectures.OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null) return Result<CoursePlayerDto>.Failure("Course not found");

        // Block access to suspended courses
        if (course.Status == CourseStatus.Suspended)
            return Result<CoursePlayerDto>.Failure("Bu kurs şu anda askıda. Lütfen daha sonra tekrar deneyin.");

        var enrollment = await _context.CourseEnrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CourseId == request.CourseId
                && e.StudentUserId == studentId
                && e.Status == CourseEnrollmentStatus.Active, cancellationToken);

        // Get all lectures flat
        var allLectures = course.Sections.SelectMany(s => s.Lectures).ToList();

        // Determine current lecture
        var currentLecture = request.LectureId.HasValue
            ? allLectures.FirstOrDefault(l => l.Id == request.LectureId.Value)
            : allLectures.FirstOrDefault();

        if (currentLecture == null) return Result<CoursePlayerDto>.Failure("No lectures found");

        // Check access: must have enrollment OR lecture is preview
        if (enrollment == null && !currentLecture.IsPreview)
            return Result<CoursePlayerDto>.Failure("Bu kursa erişmek için satın almanız gerekiyor");

        // Block access to inactive lectures
        if (!currentLecture.IsActive)
            return Result<CoursePlayerDto>.Failure("Bu ders şu anda aktif değil");

        // Get progress data
        var progressList = enrollment != null
            ? await _context.LectureProgresses
                .AsNoTracking()
                .Where(p => p.EnrollmentId == enrollment.Id)
                .ToListAsync(cancellationToken)
            : new List<Domain.Entities.LectureProgress>();

        var currentProgress = progressList.FirstOrDefault(p => p.LectureId == currentLecture.Id);

        // Generate presigned video URL (4 hours)
        string? videoUrl = null;
        if (!string.IsNullOrEmpty(currentLecture.VideoKey))
        {
            videoUrl = await _storage.GetPresignedUrlAsync(currentLecture.VideoKey, TimeSpan.FromHours(4), cancellationToken);
        }

        var sections = course.Sections.Select(s => new PlayerSectionDto(
            s.Id, s.Title,
            s.Lectures.Select(l => new PlayerLectureDto(
                l.Id, l.Title, l.DurationSec, l.IsPreview, l.Type.ToString(),
                progressList.Any(p => p.LectureId == l.Id && p.IsCompleted),
                l.IsActive
            )).ToList()
        )).ToList();

        var dto = new CoursePlayerDto(
            course.Title,
            currentLecture.Id,
            currentLecture.Title,
            currentLecture.Description,
            currentLecture.Type.ToString(),
            videoUrl,
            currentLecture.TextContent,
            currentProgress?.LastPositionSec ?? 0,
            enrollment?.CompletionPercentage ?? 0,
            sections);

        return Result<CoursePlayerDto>.Success(dto);
    }
}
