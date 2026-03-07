using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Queries.GetMyAssignments;

public record AssignmentListDto(
    Guid Id,
    string Title,
    string AssignmentType,
    string? DifficultyLevel,
    DateTime? DueDate,
    int? MaxScore,
    string Status,
    int SubmissionCount,
    int ReviewedCount,
    DateTime CreatedAt);

public record GetMyAssignmentsQuery(
    AssignmentStatus? Status = null,
    AssignmentType? AssignmentType = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<AssignmentListDto>>>;

public class GetMyAssignmentsQueryHandler : IRequestHandler<GetMyAssignmentsQuery, Result<PaginatedList<AssignmentListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyAssignmentsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<AssignmentListDto>>> Handle(GetMyAssignmentsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<AssignmentListDto>>.Failure("User not authenticated");

        var page = PaginatedList<AssignmentListDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<AssignmentListDto>.ClampPageSize(request.PageSize);

        var query = _context.Assignments
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value && !x.IsTemplate);

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value);

        if (request.AssignmentType.HasValue)
            query = query.Where(x => x.AssignmentType == request.AssignmentType.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Title.Contains(request.Search));

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AssignmentListDto(
                x.Id,
                x.Title,
                x.AssignmentType.ToString(),
                x.DifficultyLevel != null ? x.DifficultyLevel.ToString() : null,
                x.DueDate,
                x.MaxScore,
                x.Status.ToString(),
                x.Submissions.Count,
                x.Submissions.Count(s => s.Status == SubmissionStatus.Reviewed),
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<AssignmentListDto>>.Success(
            new PaginatedList<AssignmentListDto>(items, totalCount, page, pageSize));
    }
}
