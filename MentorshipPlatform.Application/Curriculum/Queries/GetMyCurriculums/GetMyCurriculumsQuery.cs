using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Queries.GetMyCurriculums;

public record CurriculumListDto(
    Guid Id,
    string Title,
    string? Description,
    string? Subject,
    string? Level,
    int TotalWeeks,
    int? EstimatedHoursPerWeek,
    string? CoverImageUrl,
    string Status,
    bool IsDefault,
    int WeekCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GetMyCurriculumsQuery(
    CurriculumStatus? Status = null,
    string? Subject = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<CurriculumListDto>>>;

public class GetMyCurriculumsQueryHandler : IRequestHandler<GetMyCurriculumsQuery, Result<PaginatedList<CurriculumListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCurriculumsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<CurriculumListDto>>> Handle(GetMyCurriculumsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<CurriculumListDto>>.Failure("User not authenticated");

        var page = PaginatedList<CurriculumListDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<CurriculumListDto>.ClampPageSize(request.PageSize);

        var query = _context.Curriculums
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value);

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Subject))
            query = query.Where(x => x.Subject == request.Subject);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Title.Contains(request.Search));

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CurriculumListDto(
                x.Id,
                x.Title,
                x.Description,
                x.Subject,
                x.Level,
                x.TotalWeeks,
                x.EstimatedHoursPerWeek,
                x.CoverImageUrl,
                x.Status.ToString(),
                x.IsDefault,
                x.Weeks.Count,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<CurriculumListDto>>.Success(
            new PaginatedList<CurriculumListDto>(items, totalCount, page, pageSize));
    }
}
