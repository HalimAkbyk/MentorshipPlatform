using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Curriculum.Queries.GetMyTemplates;

public record CurriculumTemplateDto(
    Guid Id,
    string? TemplateName,
    string Title,
    string? Description,
    string? Subject,
    string? Level,
    int TotalWeeks,
    int WeekCount,
    DateTime CreatedAt);

public record GetMyCurriculumTemplatesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<CurriculumTemplateDto>>>;

public class GetMyCurriculumTemplatesQueryHandler : IRequestHandler<GetMyCurriculumTemplatesQuery, Result<PaginatedList<CurriculumTemplateDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyCurriculumTemplatesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<CurriculumTemplateDto>>> Handle(GetMyCurriculumTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<CurriculumTemplateDto>>.Failure("User not authenticated");

        var page = PaginatedList<CurriculumTemplateDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<CurriculumTemplateDto>.ClampPageSize(request.PageSize);

        var query = _context.Curriculums
            .AsNoTracking()
            .Where(x => x.MentorUserId == _currentUser.UserId.Value && x.IsTemplate);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x =>
                (x.TemplateName != null && x.TemplateName.Contains(request.Search)) ||
                x.Title.Contains(request.Search));

        var orderedQuery = query.OrderByDescending(x => x.CreatedAt);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CurriculumTemplateDto(
                x.Id,
                x.TemplateName,
                x.Title,
                x.Description,
                x.Subject,
                x.Level,
                x.TotalWeeks,
                x.Weeks.Count,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<CurriculumTemplateDto>>.Success(
            new PaginatedList<CurriculumTemplateDto>(items, totalCount, page, pageSize));
    }
}
