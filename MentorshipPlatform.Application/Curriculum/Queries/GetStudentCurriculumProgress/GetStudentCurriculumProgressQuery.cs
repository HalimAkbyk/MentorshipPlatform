using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Queries.GetStudentCurriculumProgress;

public record TopicProgressDto(
    Guid TopicId,
    string TopicTitle,
    string Status,
    DateTime? CompletedAt,
    string? MentorNote,
    Guid? BookingId);

public record StudentCurriculumProgressDto(
    Guid EnrollmentId,
    Guid CurriculumId,
    string CurriculumTitle,
    Guid StudentUserId,
    DateTime StartedAt,
    decimal CompletionPercentage,
    string Status,
    List<TopicProgressDto> TopicProgresses);

public record GetStudentCurriculumProgressQuery(
    Guid CurriculumId,
    Guid StudentUserId) : IRequest<Result<StudentCurriculumProgressDto>>;

public class GetStudentCurriculumProgressQueryHandler : IRequestHandler<GetStudentCurriculumProgressQuery, Result<StudentCurriculumProgressDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetStudentCurriculumProgressQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<StudentCurriculumProgressDto>> Handle(GetStudentCurriculumProgressQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<StudentCurriculumProgressDto>.Failure("User not authenticated");

        var enrollment = await _context.StudentCurriculumEnrollments
            .AsNoTracking()
            .Include(x => x.Curriculum)
            .Include(x => x.TopicProgresses)
                .ThenInclude(x => x.Topic)
            .FirstOrDefaultAsync(x => x.CurriculumId == request.CurriculumId
                && x.StudentUserId == request.StudentUserId, cancellationToken);

        if (enrollment == null)
            return Result<StudentCurriculumProgressDto>.Failure("Kayit bulunamadi");

        var dto = new StudentCurriculumProgressDto(
            enrollment.Id,
            enrollment.CurriculumId,
            enrollment.Curriculum.Title,
            enrollment.StudentUserId,
            enrollment.StartedAt,
            enrollment.CompletionPercentage,
            enrollment.Status,
            enrollment.TopicProgresses.Select(tp => new TopicProgressDto(
                tp.CurriculumTopicId,
                tp.Topic.Title,
                tp.Status.ToString(),
                tp.CompletedAt,
                tp.MentorNote,
                tp.BookingId
            )).ToList());

        return Result<StudentCurriculumProgressDto>.Success(dto);
    }
}
