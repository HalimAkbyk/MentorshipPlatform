using FluentValidation;
using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Courses.Commands.GetVideoUploadUrl;

public record VideoUploadUrlResponse(string UploadUrl, string VideoKey, int ExpiresInSeconds);

public record GetVideoUploadUrlCommand(
    Guid LectureId,
    string FileName,
    string ContentType) : IRequest<Result<VideoUploadUrlResponse>>;

public class GetVideoUploadUrlCommandValidator : AbstractValidator<GetVideoUploadUrlCommand>
{
    public GetVideoUploadUrlCommandValidator()
    {
        RuleFor(x => x.LectureId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty();
    }
}

public class GetVideoUploadUrlCommandHandler : IRequestHandler<GetVideoUploadUrlCommand, Result<VideoUploadUrlResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStorageService _storage;

    public GetVideoUploadUrlCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IStorageService storage)
    {
        _context = context;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task<Result<VideoUploadUrlResponse>> Handle(GetVideoUploadUrlCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue) return Result<VideoUploadUrlResponse>.Failure("User not authenticated");

        var lecture = await _context.CourseLectures
            .Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == request.LectureId, cancellationToken);

        if (lecture == null) return Result<VideoUploadUrlResponse>.Failure("Lecture not found");
        if (lecture.Section.Course.MentorUserId != _currentUser.UserId.Value)
            return Result<VideoUploadUrlResponse>.Failure("Not authorized");

        var sanitizedFileName = request.FileName.Replace(" ", "_");
        var videoKey = $"courses/{lecture.Section.CourseId}/lectures/{lecture.Id}/{Guid.NewGuid()}_{sanitizedFileName}";
        var expiration = TimeSpan.FromHours(2);

        var uploadUrl = await _storage.GetPresignedUploadUrlAsync(videoKey, request.ContentType, expiration, cancellationToken);

        return Result<VideoUploadUrlResponse>.Success(
            new VideoUploadUrlResponse(uploadUrl, videoKey, (int)expiration.TotalSeconds));
    }
}
