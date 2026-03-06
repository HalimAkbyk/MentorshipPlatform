using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.InstructorPerformance.Queries.GetInstructorPerformanceSummary;

// DTO
public class InstructorPerformanceSummaryDto
{
    public Guid Id { get; set; }
    public Guid InstructorId { get; set; }
    public string InstructorName { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalPrivateLessons { get; set; }
    public int TotalGroupLessons { get; set; }
    public int TotalVideoViews { get; set; }
    public int TotalLiveDurationMinutes { get; set; }
    public int TotalVideoWatchMinutes { get; set; }
    public int TotalStudentsServed { get; set; }
    public int TotalCreditsConsumed { get; set; }
    public decimal TotalDirectRevenue { get; set; }
    public decimal TotalCreditRevenue { get; set; }
    public decimal PrivateLessonDemandRate { get; set; }
    public decimal GroupLessonFillRate { get; set; }
    public DateTime CalculatedAt { get; set; }
}

// Query
public record GetInstructorPerformanceSummaryQuery(
    Guid? InstructorId,
    string? PeriodType,
    DateTime? DateFrom,
    DateTime? DateTo
) : IRequest<Result<List<InstructorPerformanceSummaryDto>>>;

// Handler
public class GetInstructorPerformanceSummaryQueryHandler
    : IRequestHandler<GetInstructorPerformanceSummaryQuery, Result<List<InstructorPerformanceSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFeatureFlagService _featureFlags;

    public GetInstructorPerformanceSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IFeatureFlagService featureFlags)
    {
        _context = context;
        _currentUser = currentUser;
        _featureFlags = featureFlags;
    }

    public async Task<Result<List<InstructorPerformanceSummaryDto>>> Handle(
        GetInstructorPerformanceSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = _currentUser.IsInRole(UserRole.Admin);
        Guid targetInstructorId;

        if (request.InstructorId.HasValue)
        {
            if (!isAdmin)
                return Result<List<InstructorPerformanceSummaryDto>>.Failure("Bu işlem yalnızca admin tarafından yapılabilir.");

            targetInstructorId = request.InstructorId.Value;
        }
        else
        {
            if (!isAdmin)
            {
                // Check feature flag for self-view
                var selfViewEnabled = await _featureFlags.IsEnabledAsync(
                    FeatureFlags.InstructorPerformanceSelfView, cancellationToken);
                if (!selfViewEnabled)
                    return Result<List<InstructorPerformanceSummaryDto>>.Failure("Performans görüntüleme özelliği aktif değil.");
            }

            if (!_currentUser.UserId.HasValue)
                return Result<List<InstructorPerformanceSummaryDto>>.Failure("Kullanıcı kimliği bulunamadı.");

            targetInstructorId = _currentUser.UserId.Value;
        }

        var query = _context.InstructorPerformanceSummaries
            .AsNoTracking()
            .Where(s => s.InstructorId == targetInstructorId);

        // PeriodType filter
        if (!string.IsNullOrWhiteSpace(request.PeriodType) &&
            Enum.TryParse<PerformancePeriodType>(request.PeriodType, true, out var periodType))
        {
            query = query.Where(s => s.PeriodType == periodType);
        }

        // Date filters
        if (request.DateFrom.HasValue)
            query = query.Where(s => s.PeriodStart >= request.DateFrom.Value.ToUniversalTime());

        if (request.DateTo.HasValue)
            query = query.Where(s => s.PeriodEnd <= request.DateTo.Value.ToUniversalTime());

        var summaries = await query
            .OrderByDescending(s => s.PeriodStart)
            .Take(100)
            .ToListAsync(cancellationToken);

        // Resolve instructor name
        var instructorIds = summaries.Select(s => s.InstructorId).Distinct().ToList();
        var instructorNames = await _context.Users
            .AsNoTracking()
            .Where(u => instructorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var items = summaries.Select(s => new InstructorPerformanceSummaryDto
        {
            Id = s.Id,
            InstructorId = s.InstructorId,
            InstructorName = instructorNames.GetValueOrDefault(s.InstructorId, "Unknown"),
            PeriodType = s.PeriodType.ToString(),
            PeriodStart = s.PeriodStart,
            PeriodEnd = s.PeriodEnd,
            TotalPrivateLessons = s.TotalPrivateLessons,
            TotalGroupLessons = s.TotalGroupLessons,
            TotalVideoViews = s.TotalVideoViews,
            TotalLiveDurationMinutes = s.TotalLiveDurationMinutes,
            TotalVideoWatchMinutes = s.TotalVideoWatchMinutes,
            TotalStudentsServed = s.TotalStudentsServed,
            TotalCreditsConsumed = s.TotalCreditsConsumed,
            TotalDirectRevenue = s.TotalDirectRevenue,
            TotalCreditRevenue = s.TotalCreditRevenue,
            PrivateLessonDemandRate = s.PrivateLessonDemandRate,
            GroupLessonFillRate = s.GroupLessonFillRate,
            CalculatedAt = s.CalculatedAt
        }).ToList();

        return Result<List<InstructorPerformanceSummaryDto>>.Success(items);
    }
}
