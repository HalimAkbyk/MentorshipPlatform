using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetProcessHistory;

public record ProcessHistoryDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValue,
    string? NewValue,
    string Description,
    Guid? PerformedBy,
    string? PerformedByRole,
    string? Metadata,
    DateTime CreatedAt);

public record GetProcessHistoryQuery(
    string? EntityType,
    Guid? EntityId,
    string? Action,
    DateTime? DateFrom,
    DateTime? DateTo,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<List<ProcessHistoryDto>>>;

public class GetProcessHistoryQueryHandler
    : IRequestHandler<GetProcessHistoryQuery, Result<List<ProcessHistoryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProcessHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ProcessHistoryDto>>> Handle(
        GetProcessHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.ProcessHistories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(p => p.EntityType == request.EntityType);

        if (request.EntityId.HasValue)
            query = query.Where(p => p.EntityId == request.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(p => p.Action == request.Action);

        if (request.DateFrom.HasValue)
            query = query.Where(p => p.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(p => p.CreatedAt <= request.DateTo.Value);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProcessHistoryDto(
                p.Id,
                p.EntityType,
                p.EntityId,
                p.Action,
                p.OldValue,
                p.NewValue,
                p.Description,
                p.PerformedBy,
                p.PerformedByRole,
                p.Metadata,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<ProcessHistoryDto>>.Success(items);
    }
}
