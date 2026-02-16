using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Queries.GetPreviewLecture;

public record PreviewLectureDto(
    Guid LectureId,
    string Title,
    string? Description,
    string Type,
    string? VideoUrl,
    string? TextContent,
    int DurationSec);

public record GetPreviewLectureQuery(Guid CourseId, Guid LectureId) : IRequest<Result<PreviewLectureDto>>;

public class GetPreviewLectureQueryHandler : IRequestHandler<GetPreviewLectureQuery, Result<PreviewLectureDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IStorageService _storage;

    public GetPreviewLectureQueryHandler(IApplicationDbContext context, IStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<Result<PreviewLectureDto>> Handle(GetPreviewLectureQuery request, CancellationToken cancellationToken)
    {
        var lecture = await _context.CourseLectures
            .AsNoTracking()
            .Include(l => l.Section)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId
                && l.Section.CourseId == request.CourseId
                && l.IsPreview, cancellationToken);

        if (lecture == null)
            return Result<PreviewLectureDto>.Failure("Ders bulunamadi veya onizleme icin uygun degil");

        string? videoUrl = null;
        if (!string.IsNullOrEmpty(lecture.VideoKey))
        {
            videoUrl = await _storage.GetPresignedUrlAsync(
                lecture.VideoKey, TimeSpan.FromHours(4), cancellationToken);
        }

        return Result<PreviewLectureDto>.Success(new PreviewLectureDto(
            lecture.Id,
            lecture.Title,
            lecture.Description,
            lecture.Type.ToString(),
            videoUrl,
            lecture.TextContent,
            lecture.DurationSec));
    }
}
