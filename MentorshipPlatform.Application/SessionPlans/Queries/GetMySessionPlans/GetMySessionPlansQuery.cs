using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Queries.GetMySessionPlans;

public record SessionPlanListDto(
    Guid Id,
    string? Title,
    Guid? BookingId,
    Guid? GroupClassId,
    string Status,
    DateTime? SharedAt,
    DateTime CreatedAt,
    int MaterialCount);

public record GetMySessionPlansQuery(
    SessionPlanStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<SessionPlanListDto>>>;

public class GetMySessionPlansQueryHandler : IRequestHandler<GetMySessionPlansQuery, Result<PaginatedList<SessionPlanListDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMySessionPlansQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<SessionPlanListDto>>> Handle(GetMySessionPlansQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<SessionPlanListDto>>.Failure("User not authenticated");

        var page = PaginatedList<SessionPlanListDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<SessionPlanListDto>.ClampPageSize(request.PageSize);

        var query = _context.SessionPlans
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value && !x.IsTemplate);

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Title != null && x.Title.Contains(request.Search));

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SessionPlanListDto(
                x.Id,
                x.Title,
                x.BookingId,
                x.GroupClassId,
                x.Status.ToString(),
                x.SharedAt,
                x.CreatedAt,
                x.Materials.Count))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<SessionPlanListDto>>.Success(
            new PaginatedList<SessionPlanListDto>(items, totalCount, page, pageSize));
    }
}
