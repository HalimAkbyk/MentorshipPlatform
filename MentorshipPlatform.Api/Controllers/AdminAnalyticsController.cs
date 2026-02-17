using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using System.Globalization;
using System.Text;

namespace MentorshipPlatform.Api.Controllers;

// ────────────────────────── DTOs ──────────────────────────

public class AnalyticsOverviewDto
{
    public int TotalUsers { get; set; }
    public int TotalMentors { get; set; }
    public int TotalStudents { get; set; }
    public int ActiveUsersLast30Days { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersLastMonth { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal ThisMonthRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int TotalCourseEnrollments { get; set; }
    public int TotalGroupClassEnrollments { get; set; }
    public List<RegistrationTrendPoint> WeeklyRegistrations { get; set; } = new();
}

public class RegistrationTrendPoint
{
    public string Week { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class UserAnalyticsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int SuspendedUsers { get; set; }
    public Dictionary<string, int> RoleDistribution { get; set; } = new();
    public Dictionary<string, int> ProviderDistribution { get; set; } = new();
    public List<RegistrationTrendPoint> MonthlyRegistrations { get; set; } = new();
}

public class FinancialAnalyticsDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal PlatformCommission { get; set; }
    public decimal MentorPayouts { get; set; }
    public decimal AverageOrderAmount { get; set; }
    public Dictionary<string, decimal> RevenueByType { get; set; } = new();
    public List<MonthlyRevenuePoint> MonthlyRevenue { get; set; } = new();
}

public class MonthlyRevenuePoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Refunded { get; set; }
}

public class TopMentorDto
{
    public Guid MentorUserId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string? MentorEmail { get; set; }
    public decimal TotalEarned { get; set; }
    public int CompletedBookings { get; set; }
    public double? AverageRating { get; set; }
}

public class TopCourseDto
{
    public Guid CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string MentorName { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
    public decimal Revenue { get; set; }
}

// ────────────────────────── Controller ──────────────────────────

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminAnalyticsController(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// General overview metrics for the admin dashboard.
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var thirtyDaysAgo = now.AddDays(-30);

        // ── User counts ──
        var allUsers = await _context.Users.AsNoTracking().ToListAsync(ct);
        var totalUsers = allUsers.Count;
        var totalMentors = allUsers.Count(u => u.Roles.Contains(UserRole.Mentor));
        var totalStudents = allUsers.Count(u => u.Roles.Contains(UserRole.Student));
        var newUsersThisMonth = allUsers.Count(u => u.CreatedAt >= thisMonthStart);
        var newUsersLastMonth = allUsers.Count(u => u.CreatedAt >= lastMonthStart && u.CreatedAt < thisMonthStart);

        // ── Active users (placed orders in last 30 days) ──
        var activeUsersLast30Days = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= thirtyDaysAgo)
            .Select(o => o.BuyerUserId)
            .Distinct()
            .CountAsync(ct);

        // ── Revenue ──
        var paidOrders = _context.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid
                     || o.Status == OrderStatus.PartiallyRefunded
                     || o.Status == OrderStatus.Refunded);

        var totalRevenue = await paidOrders.SumAsync(o => (decimal?)o.AmountTotal, ct) ?? 0m;
        var thisMonthRevenue = await paidOrders
            .Where(o => o.CreatedAt >= thisMonthStart)
            .SumAsync(o => (decimal?)o.AmountTotal, ct) ?? 0m;

        // ── Booking / enrollment totals ──
        var totalBookings = await _context.Bookings.AsNoTracking().CountAsync(ct);
        var totalCourseEnrollments = await _context.CourseEnrollments.AsNoTracking().CountAsync(ct);
        var totalGroupClassEnrollments = await _context.ClassEnrollments.AsNoTracking().CountAsync(ct);

        // ── Weekly registrations (last 12 weeks) ──
        var twelveWeeksAgo = now.AddDays(-84);
        var recentUsers = allUsers.Where(u => u.CreatedAt >= twelveWeeksAgo).ToList();

        var weeklyRegistrations = new List<RegistrationTrendPoint>();
        for (int i = 11; i >= 0; i--)
        {
            var weekStart = now.AddDays(-7 * (i + 1));
            var weekEnd = now.AddDays(-7 * i);
            var count = recentUsers.Count(u => u.CreatedAt >= weekStart && u.CreatedAt < weekEnd);
            weeklyRegistrations.Add(new RegistrationTrendPoint
            {
                Week = weekStart.ToString("yyyy-MM-dd"),
                Count = count
            });
        }

        return Ok(new AnalyticsOverviewDto
        {
            TotalUsers = totalUsers,
            TotalMentors = totalMentors,
            TotalStudents = totalStudents,
            ActiveUsersLast30Days = activeUsersLast30Days,
            NewUsersThisMonth = newUsersThisMonth,
            NewUsersLastMonth = newUsersLastMonth,
            TotalRevenue = totalRevenue,
            ThisMonthRevenue = thisMonthRevenue,
            TotalBookings = totalBookings,
            TotalCourseEnrollments = totalCourseEnrollments,
            TotalGroupClassEnrollments = totalGroupClassEnrollments,
            WeeklyRegistrations = weeklyRegistrations
        });
    }

    /// <summary>
    /// User analytics: role distribution, provider distribution, monthly registrations.
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<UserAnalyticsDto>> GetUserAnalytics(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var allUsers = await _context.Users.AsNoTracking().ToListAsync(ct);

        var totalUsers = allUsers.Count;
        var activeUsers = allUsers.Count(u => u.Status == UserStatus.Active);
        var suspendedUsers = allUsers.Count(u => u.Status == UserStatus.Suspended);

        // Role distribution (a user can have multiple roles)
        var roleDistribution = new Dictionary<string, int>();
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var count = allUsers.Count(u => u.Roles.Contains(role));
            roleDistribution[role.ToString()] = count;
        }

        // Provider distribution
        var providerDistribution = allUsers
            .GroupBy(u => string.IsNullOrEmpty(u.ExternalProvider) ? "Email" : u.ExternalProvider)
            .ToDictionary(g => g.Key, g => g.Count());

        // Monthly registrations for last 12 months
        var monthlyRegistrations = new List<RegistrationTrendPoint>();
        for (int i = 11; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);
            var count = allUsers.Count(u => u.CreatedAt >= monthStart && u.CreatedAt < monthEnd);
            monthlyRegistrations.Add(new RegistrationTrendPoint
            {
                Week = monthStart.ToString("yyyy-MM"),
                Count = count
            });
        }

        return Ok(new UserAnalyticsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            SuspendedUsers = suspendedUsers,
            RoleDistribution = roleDistribution,
            ProviderDistribution = providerDistribution,
            MonthlyRegistrations = monthlyRegistrations
        });
    }

    /// <summary>
    /// Financial analytics with optional date range filter.
    /// </summary>
    [HttpGet("financial")]
    public async Task<ActionResult<FinancialAnalyticsDto>> GetFinancialAnalytics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        // ── Orders query with date filter ──
        var ordersQuery = _context.Orders.AsNoTracking().AsQueryable();
        if (dateFrom.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt <= dateTo.Value);

        var paidOrders = ordersQuery.Where(o =>
            o.Status == OrderStatus.Paid ||
            o.Status == OrderStatus.PartiallyRefunded ||
            o.Status == OrderStatus.Refunded);

        var totalRevenue = await paidOrders.SumAsync(o => (decimal?)o.AmountTotal, ct) ?? 0m;
        var totalRefunded = await paidOrders.SumAsync(o => (decimal?)o.RefundedAmount, ct) ?? 0m;
        var netRevenue = totalRevenue - totalRefunded;
        var paidCount = await paidOrders.CountAsync(ct);
        var averageOrderAmount = paidCount > 0 ? totalRevenue / paidCount : 0m;

        // ── Ledger-based metrics ──
        var ledgerQuery = _context.LedgerEntries.AsNoTracking().AsQueryable();
        if (dateFrom.HasValue)
            ledgerQuery = ledgerQuery.Where(l => l.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            ledgerQuery = ledgerQuery.Where(l => l.CreatedAt <= dateTo.Value);

        var platformCommission = await ledgerQuery
            .Where(l => l.AccountType == LedgerAccountType.Platform && l.Direction == LedgerDirection.Credit)
            .SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;

        var mentorPayouts = await ledgerQuery
            .Where(l => l.AccountType == LedgerAccountType.MentorPayout && l.Direction == LedgerDirection.Credit)
            .SumAsync(l => (decimal?)l.Amount, ct) ?? 0m;

        // ── Revenue by order type ──
        var revenueByType = await paidOrders
            .GroupBy(o => o.Type)
            .Select(g => new { Type = g.Key, Revenue = g.Sum(o => o.AmountTotal) })
            .ToListAsync(ct);

        var revenueByTypeDict = new Dictionary<string, decimal>();
        foreach (var orderType in Enum.GetValues<OrderType>())
        {
            var entry = revenueByType.FirstOrDefault(r => r.Type == orderType);
            revenueByTypeDict[orderType.ToString()] = entry?.Revenue ?? 0m;
        }

        // ── Monthly revenue for last 12 months ──
        var now = DateTime.UtcNow;
        var monthlyRevenue = new List<MonthlyRevenuePoint>();
        for (int i = 11; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);

            var monthOrders = paidOrders.Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd);
            var revenue = await monthOrders.SumAsync(o => (decimal?)o.AmountTotal, ct) ?? 0m;
            var refunded = await monthOrders.SumAsync(o => (decimal?)o.RefundedAmount, ct) ?? 0m;

            monthlyRevenue.Add(new MonthlyRevenuePoint
            {
                Month = monthStart.ToString("yyyy-MM"),
                Revenue = revenue,
                Refunded = refunded
            });
        }

        return Ok(new FinancialAnalyticsDto
        {
            TotalRevenue = totalRevenue,
            TotalRefunded = totalRefunded,
            NetRevenue = netRevenue,
            PlatformCommission = platformCommission,
            MentorPayouts = mentorPayouts,
            AverageOrderAmount = averageOrderAmount,
            RevenueByType = revenueByTypeDict,
            MonthlyRevenue = monthlyRevenue
        });
    }

    /// <summary>
    /// Top 10 mentors by earnings.
    /// </summary>
    [HttpGet("top-mentors")]
    public async Task<ActionResult<List<TopMentorDto>>> GetTopMentors(CancellationToken ct)
    {
        // Get top mentor earnings from ledger (MentorAvailable + Credit)
        var mentorEarnings = await _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountType == LedgerAccountType.MentorAvailable
                     && l.Direction == LedgerDirection.Credit
                     && l.AccountOwnerUserId != null)
            .GroupBy(l => l.AccountOwnerUserId!.Value)
            .Select(g => new { MentorUserId = g.Key, TotalEarned = g.Sum(l => l.Amount) })
            .OrderByDescending(x => x.TotalEarned)
            .Take(10)
            .ToListAsync(ct);

        var mentorIds = mentorEarnings.Select(e => e.MentorUserId).ToList();

        // Get mentor user details
        var mentorUsers = await _context.Users
            .AsNoTracking()
            .Where(u => mentorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        // Get completed booking counts per mentor
        var bookingCounts = await _context.Bookings
            .AsNoTracking()
            .Where(b => mentorIds.Contains(b.MentorUserId) && b.Status == BookingStatus.Completed)
            .GroupBy(b => b.MentorUserId)
            .Select(g => new { MentorUserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MentorUserId, x => x.Count, ct);

        // Get average ratings per mentor
        var avgRatings = await _context.Reviews
            .AsNoTracking()
            .Where(r => mentorIds.Contains(r.MentorUserId))
            .GroupBy(r => r.MentorUserId)
            .Select(g => new { MentorUserId = g.Key, AvgRating = g.Average(r => (double)r.Rating) })
            .ToDictionaryAsync(x => x.MentorUserId, x => x.AvgRating, ct);

        var result = mentorEarnings.Select(e =>
        {
            mentorUsers.TryGetValue(e.MentorUserId, out var user);
            bookingCounts.TryGetValue(e.MentorUserId, out var bc);
            avgRatings.TryGetValue(e.MentorUserId, out var ar);

            return new TopMentorDto
            {
                MentorUserId = e.MentorUserId,
                MentorName = user?.DisplayName ?? "Unknown",
                MentorEmail = user?.Email,
                TotalEarned = e.TotalEarned,
                CompletedBookings = bc,
                AverageRating = ar > 0 ? ar : null
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Top 10 courses by enrollment count.
    /// </summary>
    [HttpGet("top-courses")]
    public async Task<ActionResult<List<TopCourseDto>>> GetTopCourses(CancellationToken ct)
    {
        // Group enrollments by CourseId
        var topEnrollments = await _context.CourseEnrollments
            .AsNoTracking()
            .Where(ce => ce.Status == CourseEnrollmentStatus.Active)
            .GroupBy(ce => ce.CourseId)
            .Select(g => new { CourseId = g.Key, EnrollmentCount = g.Count() })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(10)
            .ToListAsync(ct);

        var courseIds = topEnrollments.Select(e => e.CourseId).ToList();

        // Get course details with mentor info
        var courses = await _context.Courses
            .AsNoTracking()
            .Where(c => courseIds.Contains(c.Id))
            .ToListAsync(ct);

        var mentorIds = courses.Select(c => c.MentorUserId).Distinct().ToList();
        var mentors = await _context.Users
            .AsNoTracking()
            .Where(u => mentorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        // Get revenue from course orders
        var courseRevenue = await _context.Orders
            .AsNoTracking()
            .Where(o => o.Type == OrderType.Course
                     && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.PartiallyRefunded || o.Status == OrderStatus.Refunded))
            .GroupBy(o => o.ResourceId)
            .Select(g => new { ResourceId = g.Key, Revenue = g.Sum(o => o.AmountTotal) })
            .ToListAsync(ct);

        // ResourceId in course orders maps to CourseEnrollment.Id, not CourseId.
        // We need to map enrollment IDs back to course IDs.
        var enrollmentToCourse = await _context.CourseEnrollments
            .AsNoTracking()
            .Where(ce => courseIds.Contains(ce.CourseId))
            .Select(ce => new { ce.Id, ce.CourseId })
            .ToListAsync(ct);

        var enrollmentIdToCourseId = enrollmentToCourse.ToDictionary(e => e.Id, e => e.CourseId);
        var courseRevenueDict = new Dictionary<Guid, decimal>();
        foreach (var rev in courseRevenue)
        {
            if (enrollmentIdToCourseId.TryGetValue(rev.ResourceId, out var courseId))
            {
                if (courseRevenueDict.ContainsKey(courseId))
                    courseRevenueDict[courseId] += rev.Revenue;
                else
                    courseRevenueDict[courseId] = rev.Revenue;
            }
        }

        var result = topEnrollments.Select(e =>
        {
            var course = courses.FirstOrDefault(c => c.Id == e.CourseId);
            var mentorName = "Unknown";
            if (course != null && mentors.TryGetValue(course.MentorUserId, out var mentor))
                mentorName = mentor.DisplayName;

            courseRevenueDict.TryGetValue(e.CourseId, out var revenue);

            return new TopCourseDto
            {
                CourseId = e.CourseId,
                Title = course?.Title ?? "Unknown",
                MentorName = mentorName,
                EnrollmentCount = e.EnrollmentCount,
                Revenue = revenue
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Export data as CSV. Supported types: users, orders, bookings.
    /// </summary>
    [HttpGet("export/{type}")]
    public async Task<IActionResult> ExportCsv([FromRoute] string type, CancellationToken ct)
    {
        var csv = new StringBuilder();

        switch (type.ToLowerInvariant())
        {
            case "users":
            {
                csv.AppendLine("Id,Email,DisplayName,Roles,Status,CreatedAt");
                var users = await _context.Users.AsNoTracking()
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync(ct);

                foreach (var u in users)
                {
                    var roles = string.Join(";", u.Roles.Select(r => r.ToString()));
                    csv.AppendLine($"{u.Id},{EscapeCsv(u.Email ?? "")},{EscapeCsv(u.DisplayName)},{EscapeCsv(roles)},{u.Status},{u.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                }
                break;
            }

            case "orders":
            {
                csv.AppendLine("Id,BuyerUserId,Type,AmountTotal,Status,CreatedAt");
                var orders = await _context.Orders.AsNoTracking()
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(ct);

                // Get buyer emails
                var buyerIds = orders.Select(o => o.BuyerUserId).Distinct().ToList();
                var buyerEmails = await _context.Users.AsNoTracking()
                    .Where(u => buyerIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Email ?? "", ct);

                csv.Clear();
                csv.AppendLine("Id,BuyerEmail,Type,AmountTotal,Status,CreatedAt");
                foreach (var o in orders)
                {
                    buyerEmails.TryGetValue(o.BuyerUserId, out var email);
                    csv.AppendLine($"{o.Id},{EscapeCsv(email ?? "")},{o.Type},{o.AmountTotal},{o.Status},{o.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                }
                break;
            }

            case "bookings":
            {
                csv.AppendLine("Id,MentorName,StudentName,Status,CreatedAt");
                var bookings = await _context.Bookings.AsNoTracking()
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync(ct);

                var userIds = bookings.SelectMany(b => new[] { b.MentorUserId, b.StudentUserId }).Distinct().ToList();
                var userNames = await _context.Users.AsNoTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

                foreach (var b in bookings)
                {
                    userNames.TryGetValue(b.MentorUserId, out var mentorName);
                    userNames.TryGetValue(b.StudentUserId, out var studentName);
                    csv.AppendLine($"{b.Id},{EscapeCsv(mentorName ?? "")},{EscapeCsv(studentName ?? "")},{b.Status},{b.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                }
                break;
            }

            default:
                return BadRequest(new { error = "Invalid export type. Supported types: users, orders, bookings" });
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"{type}-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
