using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Application.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Assignments.Queries.GetMyTemplates;

public record AssignmentTemplateDto(
    Guid Id,
    string? TemplateName,
    string Title,
    string AssignmentType,
    string? DifficultyLevel,
    int? EstimatedMinutes,
    int? MaxScore,
    int MaterialCount,
    DateTime CreatedAt);

public record GetMyAssignmentTemplatesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PaginatedList<AssignmentTemplateDto>>>;

public class GetMyAssignmentTemplatesQueryHandler : IRequestHandler<GetMyAssignmentTemplatesQuery, Result<PaginatedList<AssignmentTemplateDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMyAssignmentTemplatesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedList<AssignmentTemplateDto>>> Handle(GetMyAssignmentTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<PaginatedList<AssignmentTemplateDto>>.Failure("User not authenticated");

        var page = PaginatedList<AssignmentTemplateDto>.ClampPage(request.Page);
        var pageSize = PaginatedList<AssignmentTemplateDto>.ClampPageSize(request.PageSize);

        var query = _context.Assignments
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
            .Select(x => new AssignmentTemplateDto(
                x.Id,
                x.TemplateName,
                x.Title,
                x.AssignmentType.ToString(),
                x.DifficultyLevel != null ? x.DifficultyLevel.ToString() : null,
                x.EstimatedMinutes,
                x.MaxScore,
                x.Materials.Count,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginatedList<AssignmentTemplateDto>>.Success(
            new PaginatedList<AssignmentTemplateDto>(items, totalCount, page, pageSize));
    }
}
