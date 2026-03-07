using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Queries.GetMyEnrolledCurriculums;

public record EnrolledCurriculumDto(
    Guid EnrollmentId,
    Guid CurriculumId,
    string Title,
    string? Subject,
    string? Level,
    decimal CompletionPercentage,
    string Status,
    DateTime StartedAt,
    string MentorName,
    int TotalTopics,
    int CompletedTopics);

public record GetMyEnrolledCurriculumsQuery() : IRequest<Result<List<EnrolledCurriculumDto>>>;

public class GetMyEnrolledCurriculumsQueryHandler : IRequestHandler<GetMyEnrolledCurriculumsQuery, Result<List<EnrolledCurriculumDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyEnrolledCurriculumsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<EnrolledCurriculumDto>>> Handle(GetMyEnrolledCurriculumsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<EnrolledCurriculumDto>>.Failure("User not authenticated");

        var enrollments = await _context.StudentCurriculumEnrollments
            .AsNoTracking()
            .Include(x => x.Curriculum)
                .ThenInclude(x => x.Weeks)
                    .ThenInclude(x => x.Topics)
            .Include(x => x.Mentor)
            .Include(x => x.TopicProgresses)
            .Where(x => x.StudentUserId == _currentUser.UserId.Value && x.Status == "Active")
            .ToListAsync(cancellationToken);

        var dtos = enrollments.Select(e =>
        {
            var totalTopics = e.Curriculum.Weeks.SelectMany(w => w.Topics).Count();
            var completedTopics = e.TopicProgresses.Count(tp => tp.Status == TopicStatus.Completed);

            return new EnrolledCurriculumDto(
                e.Id,
                e.CurriculumId,
                e.Curriculum.Title,
                e.Curriculum.Subject,
                e.Curriculum.Level,
                e.CompletionPercentage,
                e.Status,
                e.StartedAt,
                e.Mentor.DisplayName,
                totalTopics,
                completedTopics);
        }).ToList();

        return Result<List<EnrolledCurriculumDto>>.Success(dtos);
    }
}
