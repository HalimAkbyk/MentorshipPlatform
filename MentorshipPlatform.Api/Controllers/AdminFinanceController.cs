using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Admin.Queries.GetAllUsers; // PagedResult<T>
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Persistence;

namespace MentorshipPlatform.Api.Controllers;

// ──────────── DTOs ────────────

public class AdminOrderDto
{
    public Guid Id { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerEmail { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal AmountTotal { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public string? PaymentProvider { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminOrderDetailDto
{
    public Guid Id { get; set; }
    public Guid BuyerUserId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerEmail { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public decimal AmountTotal { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public string? PaymentProvider { get; set; }
    public string? ProviderPaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LedgerEntryDto> LedgerEntries { get; set; } = new();
    public List<RefundRequestDto> RefundRequests { get; set; } = new();
}

public class LedgerEntryDto
{
    public Guid Id { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class RefundRequestDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Reason { get; set; }
    public string? AdminNotes { get; set; }
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class MentorPayoutSummaryDto
{
    public Guid MentorUserId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string? MentorEmail { get; set; }
    public decimal TotalEarned { get; set; }
    public decimal TotalPaidOut { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal InEscrow { get; set; }
    public int CompletedBookings { get; set; }
}

public class MentorPayoutDetailDto
{
    public Guid MentorUserId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public string? MentorEmail { get; set; }
    public decimal TotalEarned { get; set; }
    public decimal TotalPaidOut { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal InEscrow { get; set; }
    public int CompletedBookings { get; set; }
    public List<LedgerEntryDto> RecentTransactions { get; set; } = new();
}

public class RevenueChartDto
{
    public List<RevenueChartPoint> Points { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalPlatformFee { get; set; }
}

public class RevenueChartPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PlatformFee { get; set; }
}

public class RevenueBreakdownDto
{
    public decimal BookingRevenue { get; set; }
    public decimal GroupClassRevenue { get; set; }
    public decimal CourseRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal NetRevenue { get; set; }
}

// ──────────── Controller ────────────

[ApiController]
[Route("api/admin/finance")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminFinanceController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminFinanceController(ApplicationDbContext db)
    {
        _db = db;
    }

    // ───────── 1. GET /api/admin/finance/orders ─────────

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Orders.AsNoTracking().AsQueryable();

        // Type filter
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<OrderType>(type, true, out var orderType))
            query = query.Where(o => o.Type == orderType);

        // Status filter
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            query = query.Where(o => o.Status == orderStatus);

        // Date filters
        if (dateFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(o => o.CreatedAt <= dateTo.Value.ToUniversalTime());

        // Search filter — query matching user IDs first, then filter orders
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            var matchingUserIds = await _db.Users.AsNoTracking()
                .Where(u => u.DisplayName.ToLower().Contains(searchLower)
                    || (u.Email != null && u.Email.ToLower().Contains(searchLower)))
                .Select(u => u.Id)
                .ToListAsync();

            query = query.Where(o => matchingUserIds.Contains(o.BuyerUserId));
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.BuyerUserId,
                Type = o.Type.ToString(),
                o.AmountTotal,
                o.Currency,
                Status = o.Status.ToString(),
                o.RefundedAmount,
                o.PaymentProvider,
                o.CreatedAt
            })
            .ToListAsync();

        // Resolve buyer names/emails
        var buyerIds = orders.Select(o => o.BuyerUserId).Distinct().ToList();
        var buyers = await _db.Users.AsNoTracking()
            .Where(u => buyerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => new { u.DisplayName, u.Email });

        var items = orders.Select(o =>
        {
            var buyer = buyers.GetValueOrDefault(o.BuyerUserId);
            return new AdminOrderDto
            {
                Id = o.Id,
                BuyerName = buyer?.DisplayName ?? "Unknown",
                BuyerEmail = buyer?.Email,
                Type = o.Type,
                AmountTotal = o.AmountTotal,
                Currency = o.Currency,
                Status = o.Status,
                RefundedAmount = o.RefundedAmount,
                PaymentProvider = o.PaymentProvider,
                CreatedAt = o.CreatedAt
            };
        }).ToList();

        var result = new PagedResult<AdminOrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };

        return Ok(result);
    }

    // ───────── 2. GET /api/admin/finance/orders/{id} ─────────

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrderDetail(Guid id)
    {
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id,
                o.BuyerUserId,
                Type = o.Type.ToString(),
                o.ResourceId,
                o.AmountTotal,
                o.Currency,
                Status = o.Status.ToString(),
                o.RefundedAmount,
                o.PaymentProvider,
                o.ProviderPaymentId,
                o.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (order == null)
            return NotFound(new { error = "Order not found" });

        // Buyer info
        var buyer = await _db.Users.AsNoTracking()
            .Where(u => u.Id == order.BuyerUserId)
            .Select(u => new { u.DisplayName, u.Email })
            .FirstOrDefaultAsync();

        // Related ledger entries
        var ledgerEntries = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.ReferenceId == id)
            .OrderByDescending(le => le.CreatedAt)
            .Select(le => new LedgerEntryDto
            {
                Id = le.Id,
                AccountType = le.AccountType.ToString(),
                Direction = le.Direction.ToString(),
                Amount = le.Amount,
                ReferenceType = le.ReferenceType,
                CreatedAt = le.CreatedAt
            })
            .ToListAsync();

        // Related refund requests
        var refundRequests = await _db.RefundRequests.AsNoTracking()
            .Where(rr => rr.OrderId == id)
            .OrderByDescending(rr => rr.CreatedAt)
            .Select(rr => new RefundRequestDto
            {
                Id = rr.Id,
                Status = rr.Status.ToString(),
                RequestedAmount = rr.RequestedAmount,
                ApprovedAmount = rr.ApprovedAmount,
                Reason = rr.Reason,
                AdminNotes = rr.AdminNotes,
                Type = rr.Type.ToString(),
                CreatedAt = rr.CreatedAt,
                ProcessedAt = rr.ProcessedAt
            })
            .ToListAsync();

        var result = new AdminOrderDetailDto
        {
            Id = order.Id,
            BuyerUserId = order.BuyerUserId,
            BuyerName = buyer?.DisplayName ?? "Unknown",
            BuyerEmail = buyer?.Email,
            Type = order.Type,
            ResourceId = order.ResourceId,
            AmountTotal = order.AmountTotal,
            Currency = order.Currency,
            Status = order.Status,
            RefundedAmount = order.RefundedAmount,
            PaymentProvider = order.PaymentProvider,
            ProviderPaymentId = order.ProviderPaymentId,
            CreatedAt = order.CreatedAt,
            LedgerEntries = ledgerEntries,
            RefundRequests = refundRequests
        };

        return Ok(result);
    }

    // ───────── 3. GET /api/admin/finance/payouts/mentors ─────────

    [HttpGet("payouts/mentors")]
    public async Task<IActionResult> GetMentorPayouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var mentorQuery = _db.Users.AsNoTracking()
            .Where(u => u.Roles.Contains(UserRole.Mentor));

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            mentorQuery = mentorQuery.Where(u =>
                u.DisplayName.ToLower().Contains(searchLower)
                || (u.Email != null && u.Email.ToLower().Contains(searchLower)));
        }

        var totalCount = await mentorQuery.CountAsync();

        var mentors = await mentorQuery
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync();

        var mentorIds = mentors.Select(m => m.Id).ToList();

        // Batch query: MentorAvailable Credit entries (TotalEarned)
        var earnedByMentor = await _db.LedgerEntries.AsNoTracking()
            .Where(le => mentorIds.Contains(le.AccountOwnerUserId!.Value)
                && le.AccountType == LedgerAccountType.MentorAvailable
                && le.Direction == LedgerDirection.Credit)
            .GroupBy(le => le.AccountOwnerUserId!.Value)
            .Select(g => new { MentorId = g.Key, Total = g.Sum(le => le.Amount) })
            .ToDictionaryAsync(x => x.MentorId, x => x.Total);

        // Batch query: MentorPayout Credit entries (TotalPaidOut)
        var paidOutByMentor = await _db.LedgerEntries.AsNoTracking()
            .Where(le => mentorIds.Contains(le.AccountOwnerUserId!.Value)
                && le.AccountType == LedgerAccountType.MentorPayout
                && le.Direction == LedgerDirection.Credit)
            .GroupBy(le => le.AccountOwnerUserId!.Value)
            .Select(g => new { MentorId = g.Key, Total = g.Sum(le => le.Amount) })
            .ToDictionaryAsync(x => x.MentorId, x => x.Total);

        // Batch query: MentorEscrow net (Credit - Debit)
        var escrowCreditByMentor = await _db.LedgerEntries.AsNoTracking()
            .Where(le => mentorIds.Contains(le.AccountOwnerUserId!.Value)
                && le.AccountType == LedgerAccountType.MentorEscrow
                && le.Direction == LedgerDirection.Credit)
            .GroupBy(le => le.AccountOwnerUserId!.Value)
            .Select(g => new { MentorId = g.Key, Total = g.Sum(le => le.Amount) })
            .ToDictionaryAsync(x => x.MentorId, x => x.Total);

        var escrowDebitByMentor = await _db.LedgerEntries.AsNoTracking()
            .Where(le => mentorIds.Contains(le.AccountOwnerUserId!.Value)
                && le.AccountType == LedgerAccountType.MentorEscrow
                && le.Direction == LedgerDirection.Debit)
            .GroupBy(le => le.AccountOwnerUserId!.Value)
            .Select(g => new { MentorId = g.Key, Total = g.Sum(le => le.Amount) })
            .ToDictionaryAsync(x => x.MentorId, x => x.Total);

        // Batch query: completed bookings count
        var completedBookingsByMentor = await _db.Bookings.AsNoTracking()
            .Where(b => mentorIds.Contains(b.MentorUserId)
                && b.Status == BookingStatus.Completed)
            .GroupBy(b => b.MentorUserId)
            .Select(g => new { MentorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MentorId, x => x.Count);

        var items = mentors.Select(m =>
        {
            var totalEarned = earnedByMentor.GetValueOrDefault(m.Id, 0m);
            var totalPaidOut = paidOutByMentor.GetValueOrDefault(m.Id, 0m);
            var escrowCredit = escrowCreditByMentor.GetValueOrDefault(m.Id, 0m);
            var escrowDebit = escrowDebitByMentor.GetValueOrDefault(m.Id, 0m);

            return new MentorPayoutSummaryDto
            {
                MentorUserId = m.Id,
                MentorName = m.DisplayName,
                MentorEmail = m.Email,
                TotalEarned = totalEarned,
                TotalPaidOut = totalPaidOut,
                AvailableBalance = totalEarned - totalPaidOut,
                InEscrow = escrowCredit - escrowDebit,
                CompletedBookings = completedBookingsByMentor.GetValueOrDefault(m.Id, 0)
            };
        }).ToList();

        var result = new PagedResult<MentorPayoutSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };

        return Ok(result);
    }

    // ───────── 4. GET /api/admin/finance/payouts/mentors/{mentorUserId} ─────────

    [HttpGet("payouts/mentors/{mentorUserId:guid}")]
    public async Task<IActionResult> GetMentorPayoutDetail(Guid mentorUserId)
    {
        var mentor = await _db.Users.AsNoTracking()
            .Where(u => u.Id == mentorUserId && u.Roles.Contains(UserRole.Mentor))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .FirstOrDefaultAsync();

        if (mentor == null)
            return NotFound(new { error = "Mentor not found" });

        // TotalEarned: MentorAvailable Credit
        var totalEarned = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.AccountOwnerUserId == mentorUserId
                && le.AccountType == LedgerAccountType.MentorAvailable
                && le.Direction == LedgerDirection.Credit)
            .SumAsync(le => (decimal?)le.Amount) ?? 0m;

        // TotalPaidOut: MentorPayout Credit
        var totalPaidOut = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.AccountOwnerUserId == mentorUserId
                && le.AccountType == LedgerAccountType.MentorPayout
                && le.Direction == LedgerDirection.Credit)
            .SumAsync(le => (decimal?)le.Amount) ?? 0m;

        // InEscrow: MentorEscrow Credit - Debit
        var escrowCredit = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.AccountOwnerUserId == mentorUserId
                && le.AccountType == LedgerAccountType.MentorEscrow
                && le.Direction == LedgerDirection.Credit)
            .SumAsync(le => (decimal?)le.Amount) ?? 0m;

        var escrowDebit = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.AccountOwnerUserId == mentorUserId
                && le.AccountType == LedgerAccountType.MentorEscrow
                && le.Direction == LedgerDirection.Debit)
            .SumAsync(le => (decimal?)le.Amount) ?? 0m;

        // Completed bookings
        var completedBookings = await _db.Bookings.AsNoTracking()
            .CountAsync(b => b.MentorUserId == mentorUserId
                && b.Status == BookingStatus.Completed);

        // Recent transactions (last 50 ledger entries for this mentor)
        var recentTransactions = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.AccountOwnerUserId == mentorUserId)
            .OrderByDescending(le => le.CreatedAt)
            .Take(50)
            .Select(le => new LedgerEntryDto
            {
                Id = le.Id,
                AccountType = le.AccountType.ToString(),
                Direction = le.Direction.ToString(),
                Amount = le.Amount,
                ReferenceType = le.ReferenceType,
                CreatedAt = le.CreatedAt
            })
            .ToListAsync();

        var result = new MentorPayoutDetailDto
        {
            MentorUserId = mentor.Id,
            MentorName = mentor.DisplayName,
            MentorEmail = mentor.Email,
            TotalEarned = totalEarned,
            TotalPaidOut = totalPaidOut,
            AvailableBalance = totalEarned - totalPaidOut,
            InEscrow = escrowCredit - escrowDebit,
            CompletedBookings = completedBookings,
            RecentTransactions = recentTransactions
        };

        return Ok(result);
    }

    // ───────── 5. GET /api/admin/finance/revenue/chart ─────────

    [HttpGet("revenue/chart")]
    public async Task<IActionResult> GetRevenueChart(
        [FromQuery] string period = "daily",
        [FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var startDate = DateTime.UtcNow.AddDays(-days);

        // Paid orders within date range
        var paidOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid && o.CreatedAt >= startDate)
            .Select(o => new { o.AmountTotal, o.CreatedAt })
            .ToListAsync();

        // Platform fee entries within date range
        var platformFees = await _db.LedgerEntries.AsNoTracking()
            .Where(le => le.AccountType == LedgerAccountType.Platform
                && le.Direction == LedgerDirection.Credit
                && le.CreatedAt >= startDate)
            .Select(le => new { le.Amount, le.CreatedAt })
            .ToListAsync();

        // Group by period
        var revenueGrouped = period.ToLower() switch
        {
            "weekly" => paidOrders
                .GroupBy(o => GetWeekLabel(o.CreatedAt))
                .ToDictionary(g => g.Key, g => g.Sum(o => o.AmountTotal)),
            "monthly" => paidOrders
                .GroupBy(o => o.CreatedAt.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(o => o.AmountTotal)),
            _ => paidOrders // daily
                .GroupBy(o => o.CreatedAt.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Sum(o => o.AmountTotal))
        };

        var feeGrouped = period.ToLower() switch
        {
            "weekly" => platformFees
                .GroupBy(f => GetWeekLabel(f.CreatedAt))
                .ToDictionary(g => g.Key, g => g.Sum(f => f.Amount)),
            "monthly" => platformFees
                .GroupBy(f => f.CreatedAt.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(f => f.Amount)),
            _ => platformFees
                .GroupBy(f => f.CreatedAt.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Sum(f => f.Amount))
        };

        // Merge all labels and sort
        var allLabels = revenueGrouped.Keys.Union(feeGrouped.Keys).OrderBy(l => l).ToList();

        var points = allLabels.Select(label => new RevenueChartPoint
        {
            Label = label,
            Revenue = revenueGrouped.GetValueOrDefault(label, 0m),
            PlatformFee = feeGrouped.GetValueOrDefault(label, 0m)
        }).ToList();

        var result = new RevenueChartDto
        {
            Points = points,
            TotalRevenue = paidOrders.Sum(o => o.AmountTotal),
            TotalPlatformFee = platformFees.Sum(f => f.Amount)
        };

        return Ok(result);
    }

    // ───────── 6. GET /api/admin/finance/revenue/breakdown ─────────

    [HttpGet("revenue/breakdown")]
    public async Task<IActionResult> GetRevenueBreakdown(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null)
    {
        var query = _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid
                || o.Status == OrderStatus.Refunded
                || o.Status == OrderStatus.PartiallyRefunded);

        if (dateFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(o => o.CreatedAt <= dateTo.Value.ToUniversalTime());

        var grouped = await query
            .GroupBy(o => o.Type)
            .Select(g => new
            {
                Type = g.Key,
                Revenue = g.Sum(o => o.AmountTotal),
                Refunded = g.Sum(o => o.RefundedAmount)
            })
            .ToListAsync();

        var bookingRevenue = grouped
            .Where(g => g.Type == OrderType.Booking)
            .Select(g => g.Revenue)
            .FirstOrDefault();

        var groupClassRevenue = grouped
            .Where(g => g.Type == OrderType.GroupClass)
            .Select(g => g.Revenue)
            .FirstOrDefault();

        var courseRevenue = grouped
            .Where(g => g.Type == OrderType.Course)
            .Select(g => g.Revenue)
            .FirstOrDefault();

        var totalRevenue = grouped.Sum(g => g.Revenue);
        var totalRefunded = grouped.Sum(g => g.Refunded);

        var result = new RevenueBreakdownDto
        {
            BookingRevenue = bookingRevenue,
            GroupClassRevenue = groupClassRevenue,
            CourseRevenue = courseRevenue,
            TotalRevenue = totalRevenue,
            TotalRefunded = totalRefunded,
            NetRevenue = totalRevenue - totalRefunded
        };

        return Ok(result);
    }

    // ──────────── Helpers ────────────

    private static string GetWeekLabel(DateTime date)
    {
        // ISO 8601 week: use Monday as start of week
        var day = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        var weekStart = date.AddDays(-day);
        return weekStart.ToString("yyyy-MM-dd");
    }
}
