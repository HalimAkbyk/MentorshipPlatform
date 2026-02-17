using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetAdminDashboard;

// DTOs
public class AdminDashboardDto
{
    // User stats
    public int TotalUsers { get; set; }
    public int TotalMentors { get; set; }
    public int TotalStudents { get; set; }
    public int ActiveUsersLast30Days { get; set; }
    public int NewUsersThisWeek { get; set; }
    public int NewUsersLastWeek { get; set; }

    // Revenue stats
    public decimal ThisMonthRevenue { get; set; }
    public decimal LastMonthRevenue { get; set; }
    public decimal RevenueChangePercent { get; set; }

    // Pending actions
    public int PendingVerifications { get; set; }
    public int PendingRefunds { get; set; }
    public int ActiveDisputes { get; set; }
    public int PendingOrders { get; set; }

    // Trends (last 30 days)
    public List<DailyStatDto> WeeklyRegistrations { get; set; } = new();
    public List<DailyStatDto> DailyRevenue { get; set; } = new();

    // Recent activity
    public List<RecentActivityDto> RecentActivities { get; set; } = new();
}

public class DailyStatDto
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}

public class RecentActivityDto
{
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Query
public record GetAdminDashboardQuery : IRequest<Result<AdminDashboardDto>>;

// Handler
public class GetAdminDashboardQueryHandler
    : IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<AdminDashboardDto>> Handle(
        GetAdminDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Date boundaries
        var thirtyDaysAgo = today.AddDays(-30);
        var thisWeekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        if (today.DayOfWeek == DayOfWeek.Sunday)
            thisWeekStart = thisWeekStart.AddDays(-7);
        var lastWeekStart = thisWeekStart.AddDays(-7);
        var thisMonthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // ---- User Stats ----
        var totalUsers = await _context.Users
            .CountAsync(cancellationToken);

        var totalMentors = await _context.Users
            .CountAsync(u => u.Roles.Contains(UserRole.Mentor), cancellationToken);

        var totalStudents = await _context.Users
            .CountAsync(u => u.Roles.Contains(UserRole.Student), cancellationToken);

        var activeUsersLast30Days = await _context.Users
            .CountAsync(u => u.UpdatedAt >= thirtyDaysAgo, cancellationToken);

        var newUsersThisWeek = await _context.Users
            .CountAsync(u => u.CreatedAt >= thisWeekStart, cancellationToken);

        var newUsersLastWeek = await _context.Users
            .CountAsync(u => u.CreatedAt >= lastWeekStart && u.CreatedAt < thisWeekStart,
                cancellationToken);

        // ---- Revenue Stats ----
        // Revenue = Platform account Credit entries
        var thisMonthRevenue = await _context.LedgerEntries
            .Where(e => e.AccountType == LedgerAccountType.Platform
                        && e.Direction == LedgerDirection.Credit
                        && e.CreatedAt >= thisMonthStart)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var lastMonthRevenue = await _context.LedgerEntries
            .Where(e => e.AccountType == LedgerAccountType.Platform
                        && e.Direction == LedgerDirection.Credit
                        && e.CreatedAt >= lastMonthStart
                        && e.CreatedAt < thisMonthStart)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var revenueChangePercent = lastMonthRevenue > 0
            ? Math.Round((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue * 100, 2)
            : (thisMonthRevenue > 0 ? 100m : 0m);

        // ---- Pending Actions ----
        var pendingVerifications = await _context.MentorVerifications
            .CountAsync(v => v.Status == VerificationStatus.Pending, cancellationToken);

        var pendingRefunds = await _context.RefundRequests
            .CountAsync(r => r.Status == RefundRequestStatus.Pending, cancellationToken);

        var activeDisputes = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Disputed, cancellationToken);

        var pendingOrders = await _context.Orders
            .CountAsync(o => o.Status == OrderStatus.Pending, cancellationToken);

        // ---- Trends: Weekly Registrations (last 30 days, grouped by day) ----
        var weeklyRegistrations = await _context.Users
            .Where(u => u.CreatedAt >= thirtyDaysAgo)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new DailyStatDto
            {
                Date = g.Key,
                Value = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        // ---- Trends: Daily Revenue (last 30 days) ----
        var dailyRevenue = await _context.LedgerEntries
            .Where(e => e.AccountType == LedgerAccountType.Platform
                        && e.Direction == LedgerDirection.Credit
                        && e.CreatedAt >= thirtyDaysAgo)
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new DailyStatDto
            {
                Date = g.Key,
                Value = g.Sum(e => e.Amount)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        // ---- Recent Activities (last 10 from ProcessHistories) ----
        var recentProcessHistories = await _context.ProcessHistories
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        // Resolve performer display names
        var performerIds = recentProcessHistories
            .Where(p => p.PerformedBy.HasValue)
            .Select(p => p.PerformedBy!.Value)
            .Distinct()
            .ToList();

        var performerNames = performerIds.Count > 0
            ? await _context.Users
                .AsNoTracking()
                .Where(u => performerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken)
            : new Dictionary<Guid, string>();

        var recentActivities = recentProcessHistories.Select(p => new RecentActivityDto
        {
            Action = p.Action,
            EntityType = p.EntityType,
            Description = p.Description,
            PerformedBy = p.PerformedBy.HasValue && performerNames.TryGetValue(p.PerformedBy.Value, out var name)
                ? name
                : p.PerformedByRole ?? "System",
            CreatedAt = p.CreatedAt
        }).ToList();

        // ---- Build DTO ----
        var dto = new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalMentors = totalMentors,
            TotalStudents = totalStudents,
            ActiveUsersLast30Days = activeUsersLast30Days,
            NewUsersThisWeek = newUsersThisWeek,
            NewUsersLastWeek = newUsersLastWeek,

            ThisMonthRevenue = thisMonthRevenue,
            LastMonthRevenue = lastMonthRevenue,
            RevenueChangePercent = revenueChangePercent,

            PendingVerifications = pendingVerifications,
            PendingRefunds = pendingRefunds,
            ActiveDisputes = activeDisputes,
            PendingOrders = pendingOrders,

            WeeklyRegistrations = weeklyRegistrations,
            DailyRevenue = dailyRevenue,

            RecentActivities = recentActivities
        };

        return Result<AdminDashboardDto>.Success(dto);
    }
}
