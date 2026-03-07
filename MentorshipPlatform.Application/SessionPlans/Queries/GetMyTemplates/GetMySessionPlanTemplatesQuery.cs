using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.SessionPlans.Queries.GetMyTemplates;

public record SessionPlanTemplateDto(
    Guid Id,
    string? TemplateName,
    string? Title,
    string? SessionObjective,
    int MaterialCount,
    DateTime CreatedAt);

public record GetMySessionPlanTemplatesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<SessionPlanTemplateDto>>>;

public class GetMySessionPlanTemplatesQueryHandler : IRequestHandler<GetMySessionPlanTemplatesQuery, Result<PaginatedList<SessionPlanTemplateDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMySessionPlanTemplatesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<SessionPlanTemplateDto>>> Handle(GetMySessionPlanTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<SessionPlanTemplateDto>>.Failure("User not authenticated");

        var page = PaginatedList<SessionPlanTemplateDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<SessionPlanTemplateDto>.ClampPageSize(request.PageSize);

        var query = _context.SessionPlans
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value && x.IsTemplate);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x =>
                (x.TemplateName != null && x.TemplateName.Contains(request.Search)) ||
                (x.Title != null && x.Title.Contains(request.Search)));

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SessionPlanTemplateDto(
                x.Id,
                x.TemplateName,
                x.Title,
                x.SessionObjective,
                x.Materials.Count,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<SessionPlanTemplateDto>>.Success(
            new PaginatedList<SessionPlanTemplateDto>(items, totalCount, page, pageSize));
    }
}
