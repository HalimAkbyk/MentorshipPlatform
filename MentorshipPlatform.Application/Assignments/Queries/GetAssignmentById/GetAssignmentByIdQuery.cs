using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Queries.GetAssignmentById;

public record ReviewDto(
    int? Score,
    string? Feedback,
    string Status,
    DateTime ReviewedAt);

public record SubmissionDto(
    Guid Id,
    Guid StudentUserId,
    string StudentName,
    string? SubmissionText,
    string? FileUrl,
    DateTime SubmittedAt,
    bool IsLate,
    string Status,
    ReviewDto? Review);

public record AssignmentMaterialDto(
    Guid Id,
    Guid LibraryItemId,
    string LibraryItemTitle,
    string? LibraryItemFileUrl,
    int SortOrder,
    bool IsRequired);

public record AssignmentDetailDto(
    Guid Id,
    Guid MentorUserId,
    string MentorName,
    string Title,
    string? Description,
    string? Instructions,
    string AssignmentType,
    string? DifficultyLevel,
    int? EstimatedMinutes,
    DateTime? DueDate,
    int? MaxScore,
    bool AllowLateSubmission,
    int? LatePenaltyPercent,
    Guid? BookingId,
    Guid? GroupClassId,
    Guid? CurriculumTopicId,
    string Status,
    AssignmentMaterialDto[] Materials,
    SubmissionDto[] Submissions,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GetAssignmentByIdQuery(Guid Id) : IRequest<Result<AssignmentDetailDto?>>;

public class GetAssignmentByIdQueryHandler : IRequestHandler<GetAssignmentByIdQuery, Result<AssignmentDetailDto?>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetAssignmentByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<AssignmentDetailDto?>> Handle(GetAssignmentByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<AssignmentDetailDto?>.Failure("User not authenticated");

        var assignment = await _context.Assignments
            .AsNoTracking()
            .Include(a => a.Mentor)
            .Include(a => a.Materials)
                .ThenInclude(m => m.LibraryItem)
            .Include(a => a.Submissions)
                .ThenInclude(s => s.Student)
            .Include(a => a.Submissions)
                .ThenInclude(s => s.Review)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (assignment == null)
            return Result<AssignmentDetailDto?>.Success(null);

        var dto = new AssignmentDetailDto(
            assignment.Id,
            assignment.MentorUserId,
            assignment.Mentor.DisplayName,
            assignment.Title,
            assignment.Description,
            assignment.Instructions,
            assignment.AssignmentType.ToString(),
            assignment.DifficultyLevel?.ToString(),
            assignment.EstimatedMinutes,
            assignment.DueDate,
            assignment.MaxScore,
            assignment.AllowLateSubmission,
            assignment.LatePenaltyPercent,
            assignment.BookingId,
            assignment.GroupClassId,
            assignment.CurriculumTopicId,
            assignment.Status.ToString(),
            assignment.Materials.OrderBy(m => m.SortOrder).Select(m => new AssignmentMaterialDto(
                m.Id,
                m.LibraryItemId,
                m.LibraryItem.Title,
                m.LibraryItem.FileUrl,
                m.SortOrder,
                m.IsRequired)).ToArray(),
            assignment.Submissions.OrderByDescending(s => s.SubmittedAt).Select(s => new SubmissionDto(
                s.Id,
                s.StudentUserId,
                s.Student.DisplayName,
                s.SubmissionText,
                s.FileUrl,
                s.SubmittedAt,
                s.IsLate,
                s.Status.ToString(),
                s.Review != null ? new ReviewDto(
                    s.Review.Score,
                    s.Review.Feedback,
                    s.Review.Status.ToString(),
                    s.Review.ReviewedAt) : null)).ToArray(),
            assignment.CreatedAt,
            assignment.UpdatedAt);

        return Result<AssignmentDetailDto?>.Success(dto);
    }
}
