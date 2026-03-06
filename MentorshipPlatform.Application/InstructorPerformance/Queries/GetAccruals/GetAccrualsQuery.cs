using MediatR;
using MentorshipPlatform.Application.Admin.Queries.GetAllUsers;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Queries.GetAccruals;

// DTO
public class AccrualDto
{
    public Guid Id { get; set; }
    public Guid InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int PrivateLessonCount { get; set; }
    public decimal PrivateLessonUnitPrice { get; set; }
    public int GroupLessonCount { get; set; }
    public decimal GroupLessonUnitPrice { get; set; }
    public int VideoContentCount { get; set; }
    public decimal VideoUnitPrice { get; set; }
    public decimal BonusAmount { get; set; }
    public string? BonusDescription { get; set; }
    public decimal TotalAccrual { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Query
public record GetAccrualsQuery(
    Guid? InstructorId,
    string? Status,
    int Page,
    int PageSize
) : IRequest<Result<PagedResult<AccrualDto>>>;

// Handler
public class GetAccrualsQueryHandler
    : IRequestHandler<GetAccrualsQuery, Result<PagedResult<AccrualDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureFlagService _featureFlags;

    public GetAccrualsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IFeatureFlagService featureFlags)
    {
        _context = context;
        _currentUser = currentUser;
        _featureFlags = featureFlags;
    }

    public async Task<Result<PagedResult<AccrualDto>>> Handle(
        GetAccrualsQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = _currentUser.IsInRole(UserRole.Admin);

        var query = _context.InstructorAccruals
            .AsNoTracking()
            .AsQueryable();

        if (isAdmin)
        {
            // Admin can filter by instructor
            if (request.InstructorId.HasValue)
                query = query.Where(a => a.InstructorId == request.InstructorId.Value);
        }
        else
        {
            // Non-admin: check feature flag and show own data only
            var selfViewEnabled = await _featureFlags.IsEnabledAsync(
                FeatureFlags.InstructorAccrualSelfView, cancellationToken);
            if (!selfViewEnabled)
                return Result<PagedResult<AccrualDto>>.Failure("Hakediş görüntüleme özelliği aktif değil.");

            if (!_currentUser.UserId.HasValue)
                return Result<PagedResult<AccrualDto>>.Failure("Kullanıcı kimliği bulunamadı.");

            query = query.Where(a => a.InstructorId == _currentUser.UserId.Value);
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<AccrualStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(a => a.Status == statusFilter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var accruals = await query
            .OrderByDescending(a => a.PeriodStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Resolve instructor names
        var instructorIds = accruals.Select(a => a.InstructorId).Distinct().ToList();
        var instructorNames = await _context.Users
            .AsNoTracking()
            .Where(u => instructorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var items = accruals.Select(a => new AccrualDto
        {
            Id = a.Id,
            InstructorId = a.InstructorId,
            InstructorName = instructorNames.GetValueOrDefault(a.InstructorId, "Unknown"),
            PeriodStart = a.PeriodStart,
            PeriodEnd = a.PeriodEnd,
            PrivateLessonCount = a.PrivateLessonCount,
            PrivateLessonUnitPrice = a.PrivateLessonUnitPrice,
            GroupLessonCount = a.GroupLessonCount,
            GroupLessonUnitPrice = a.GroupLessonUnitPrice,
            VideoContentCount = a.VideoContentCount,
            VideoUnitPrice = a.VideoUnitPrice,
            BonusAmount = a.BonusAmount,
            BonusDescription = a.BonusDescription,
            TotalAccrual = a.TotalAccrual,
            Status = a.Status.ToString(),
            ApprovedBy = a.ApprovedBy,
            ApprovedAt = a.ApprovedAt,
            PaidAt = a.PaidAt,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt
        }).ToList();

        var result = new PagedResult<AccrualDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };

        return Result<PagedResult<AccrualDto>>.Success(result);
    }
}
