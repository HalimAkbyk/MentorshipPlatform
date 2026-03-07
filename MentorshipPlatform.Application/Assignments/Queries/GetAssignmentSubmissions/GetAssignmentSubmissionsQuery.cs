using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Queries.GetAssignmentSubmissions;

public record SubmissionReviewDto(
    int? Score,
    string? Feedback,
    string Status,
    DateTime ReviewedAt);

public record AssignmentSubmissionDto(
    Guid Id,
    Guid StudentUserId,
    string StudentName,
    string? SubmissionText,
    string? FileUrl,
    string? OriginalFileName,
    DateTime SubmittedAt,
    bool IsLate,
    string Status,
    SubmissionReviewDto? Review);

public record GetAssignmentSubmissionsQuery(Guid AssignmentId) : IRequest<Result<List<AssignmentSubmissionDto>>>;

public class GetAssignmentSubmissionsQueryHandler : IRequestHandler<GetAssignmentSubmissionsQuery, Result<List<AssignmentSubmissionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAssignmentSubmissionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<List<AssignmentSubmissionDto>>> Handle(GetAssignmentSubmissionsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<List<AssignmentSubmissionDto>>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AssignmentId, cancellationToken);

        if (assignment == null)
            return Result<List<AssignmentSubmissionDto>>.Failure("Odev bulunamadi");

        if (assignment.MentorUserId != _currentUser.UserId.Value)
            return Result<List<AssignmentSubmissionDto>>.Failure("Bu odevin teslimlerini goruntuleme yetkiniz yok");

        var submissions = await _context.AssignmentSubmissions
            .AsNoTracking()
            .Where(s => s.AssignmentId == request.AssignmentId)
            .Include(s => s.Student)
            .Include(s => s.Review)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new AssignmentSubmissionDto(
                s.Id,
                s.StudentUserId,
                s.Student.DisplayName,
                s.SubmissionText,
                s.FileUrl,
                s.OriginalFileName,
                s.SubmittedAt,
                s.IsLate,
                s.Status.ToString(),
                s.Review != null ? new SubmissionReviewDto(
                    s.Review.Score,
                    s.Review.Feedback,
                    s.Review.Status.ToString(),
                    s.Review.ReviewedAt) : null))
            .ToListAsync(cancellationToken);

        return Result<List<AssignmentSubmissionDto>>.Success(submissions);
    }
}
