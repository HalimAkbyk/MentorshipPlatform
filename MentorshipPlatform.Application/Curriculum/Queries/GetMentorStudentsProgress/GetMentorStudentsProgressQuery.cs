using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Queries.GetMentorStudentsProgress;

public record StudentProgressSummaryDto(
    Guid EnrollmentId,
    Guid StudentUserId,
    string StudentName,
    Guid CurriculumId,
    string CurriculumTitle,
    decimal CompletionPercentage,
    string Status,
    DateTime StartedAt,
    int TotalTopics,
    int CompletedTopics);

public record GetMentorStudentsProgressQuery(Guid? CurriculumId) : IRequest<Result<List<StudentProgressSummaryDto>>>;

public class GetMentorStudentsProgressQueryHandler : IRequestHandler<GetMentorStudentsProgressQuery, Result<List<StudentProgressSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorStudentsProgressQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<StudentProgressSummaryDto>>> Handle(GetMentorStudentsProgressQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<StudentProgressSummaryDto>>.Failure("User not authenticated");

        var query = _context.StudentCurriculumEnrollments
            .AsNoTracking()
            .Include(x => x.Curriculum)
                .ThenInclude(x => x.Weeks)
                    .ThenInclude(x => x.Topics)
            .Include(x => x.Student)
            .Include(x => x.TopicProgresses)
            .Where(x => x.MentorUserId == _currentUser.UserId.Value);

        if (request.CurriculumId.HasValue)
            query = query.Where(x => x.CurriculumId == request.CurriculumId.Value);

        var enrollments = await query.ToListAsync(cancellationToken);

        var dtos = enrollments.Select(e =>
        {
            var totalTopics = e.Curriculum.Weeks.SelectMany(w => w.Topics).Count();
            var completedTopics = e.TopicProgresses.Count(tp => tp.Status == TopicStatus.Completed);

            return new StudentProgressSummaryDto(
                e.Id,
                e.StudentUserId,
                e.Student.DisplayName,
                e.CurriculumId,
                e.Curriculum.Title,
                e.CompletionPercentage,
                e.Status,
                e.StartedAt,
                totalTopics,
                completedTopics);
        }).ToList();

        return Result<List<StudentProgressSummaryDto>>.Success(dtos);
    }
}
