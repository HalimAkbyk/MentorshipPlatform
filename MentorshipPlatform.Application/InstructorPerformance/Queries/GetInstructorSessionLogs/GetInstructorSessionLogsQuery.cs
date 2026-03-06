using MediatR;
using MentorshipPlatform.Application.Admin.Queries.GetAllUsers;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Queries.GetInstructorSessionLogs;

// DTO
public class InstructorSessionLogDto
{
    public Guid Id { get; set; }
    public Guid InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public Guid? VideoParticipantId { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public int? DurationMinutes { get; set; }
}

// Query
public record GetInstructorSessionLogsQuery(
    Guid? InstructorId,
    int Page,
    int PageSize,
    string? SessionType,
    DateTime? DateFrom,
    DateTime? DateTo
) : IRequest<Result<PagedResult<InstructorSessionLogDto>>>;

// Handler
public class GetInstructorSessionLogsQueryHandler
    : IRequestHandler<GetInstructorSessionLogsQuery, Result<PagedResult<InstructorSessionLogDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetInstructorSessionLogsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<InstructorSessionLogDto>>> Handle(
        GetInstructorSessionLogsQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = _currentUser.IsInRole(UserRole.Admin);
        Guid targetInstructorId;

        if (request.InstructorId.HasValue)
        {
            if (!isAdmin)
                return Result<PagedResult<InstructorSessionLogDto>>.Failure("Bu işlem yalnızca admin tarafından yapılabilir.");

            targetInstructorId = request.InstructorId.Value;
        }
        else
        {
            if (!_currentUser.UserId.HasValue)
                return Result<PagedResult<InstructorSessionLogDto>>.Failure("Kullanıcı kimliği bulunamadı.");

            targetInstructorId = _currentUser.UserId.Value;
        }

        var query = _context.InstructorSessionLogs
            .AsNoTracking()
            .Where(sl => sl.InstructorId == targetInstructorId);

        // SessionType filter
        if (!string.IsNullOrWhiteSpace(request.SessionType) &&
            Enum.TryParse<Domain.Enums.SessionType>(request.SessionType, true, out var sessionType))
        {
            query = query.Where(sl => sl.SessionType == sessionType);
        }

        // Date filters
        if (request.DateFrom.HasValue)
            query = query.Where(sl => sl.JoinedAt >= request.DateFrom.Value.ToUniversalTime());

        if (request.DateTo.HasValue)
            query = query.Where(sl => sl.JoinedAt <= request.DateTo.Value.ToUniversalTime());

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var logs = await query
            .OrderByDescending(sl => sl.JoinedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Resolve instructor names
        var instructorIds = logs.Select(sl => sl.InstructorId).Distinct().ToList();
        var instructorNames = await _context.Users
            .AsNoTracking()
            .Where(u => instructorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var items = logs.Select(sl => new InstructorSessionLogDto
        {
            Id = sl.Id,
            InstructorId = sl.InstructorId,
            InstructorName = instructorNames.GetValueOrDefault(sl.InstructorId, "Unknown"),
            SessionType = sl.SessionType.ToString(),
            SessionId = sl.SessionId,
            VideoParticipantId = sl.VideoParticipantId,
            JoinedAt = sl.JoinedAt,
            LeftAt = sl.LeftAt,
            DurationMinutes = sl.DurationMinutes
        }).ToList();

        var result = new PagedResult<InstructorSessionLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };

        return Result<PagedResult<InstructorSessionLogDto>>.Success(result);
    }
}
