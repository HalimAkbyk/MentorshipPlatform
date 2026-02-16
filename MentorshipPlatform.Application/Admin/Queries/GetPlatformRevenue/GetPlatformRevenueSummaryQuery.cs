using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Admin.Queries.GetPlatformRevenue;

public record PlatformRevenueSummaryDto(
    decimal TotalRevenue,
    decimal TotalRefundsIssued,
    decimal NetRevenue,
    decimal TotalMentorEarnings,
    decimal TotalGrossVolume,
    int TotalOrders,
    int TotalRefunds,
    decimal ThisMonthRevenue,
    decimal LastMonthRevenue);

public record GetPlatformRevenueSummaryQuery(
    DateTime? From = null,
    DateTime? To = null
) : IRequest<Result<PlatformRevenueSummaryDto>>;

public class GetPlatformRevenueSummaryQueryHandler
    : IRequestHandler<GetPlatformRevenueSummaryQuery, Result<PlatformRevenueSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPlatformRevenueSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PlatformRevenueSummaryDto>> Handle(
        GetPlatformRevenueSummaryQuery request,
        CancellationToken cancellationToken)
    {
        // Platform ledger entries
        var platformEntries = _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountType == LedgerAccountType.Platform);

        if (request.From.HasValue)
            platformEntries = platformEntries.Where(l => l.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            platformEntries = platformEntries.Where(l => l.CreatedAt <= request.To.Value);

        var platformCredits = await platformEntries
            .Where(l => l.Direction == LedgerDirection.Credit)
            .SumAsync(l => l.Amount, cancellationToken);

        var platformDebits = await platformEntries
            .Where(l => l.Direction == LedgerDirection.Debit)
            .SumAsync(l => l.Amount, cancellationToken);

        var totalRevenue = platformCredits;
        var netRevenue = platformCredits - platformDebits;

        // Student refunds issued
        var refundEntries = _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountType == LedgerAccountType.StudentRefund
                && l.Direction == LedgerDirection.Credit);

        if (request.From.HasValue)
            refundEntries = refundEntries.Where(l => l.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            refundEntries = refundEntries.Where(l => l.CreatedAt <= request.To.Value);

        var totalRefundsIssued = await refundEntries.SumAsync(l => l.Amount, cancellationToken);

        // Mentor earnings (MentorAvailable + MentorEscrow net)
        var mentorEntries = _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountType == LedgerAccountType.MentorEscrow
                || l.AccountType == LedgerAccountType.MentorAvailable);

        if (request.From.HasValue)
            mentorEntries = mentorEntries.Where(l => l.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            mentorEntries = mentorEntries.Where(l => l.CreatedAt <= request.To.Value);

        var mentorCredits = await mentorEntries
            .Where(l => l.Direction == LedgerDirection.Credit)
            .SumAsync(l => l.Amount, cancellationToken);

        var mentorDebits = await mentorEntries
            .Where(l => l.Direction == LedgerDirection.Debit)
            .SumAsync(l => l.Amount, cancellationToken);

        var totalMentorEarnings = mentorCredits - mentorDebits;

        // Order stats
        var ordersQuery = _context.Orders.AsNoTracking();
        if (request.From.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CreatedAt <= request.To.Value);

        var totalGrossVolume = await ordersQuery
            .Where(o => o.Status == OrderStatus.Paid
                || o.Status == OrderStatus.Refunded
                || o.Status == OrderStatus.PartiallyRefunded)
            .SumAsync(o => o.AmountTotal, cancellationToken);

        var totalOrders = await ordersQuery
            .Where(o => o.Status != OrderStatus.Pending && o.Status != OrderStatus.Failed)
            .CountAsync(cancellationToken);

        var totalRefunds = await _context.RefundRequests
            .AsNoTracking()
            .Where(r => r.Status == RefundRequestStatus.Approved)
            .CountAsync(cancellationToken);

        // This month / last month platform revenue
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        var thisMonthRevenue = await _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountType == LedgerAccountType.Platform
                && l.Direction == LedgerDirection.Credit
                && l.CreatedAt >= thisMonthStart)
            .SumAsync(l => l.Amount, cancellationToken);

        var lastMonthRevenue = await _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountType == LedgerAccountType.Platform
                && l.Direction == LedgerDirection.Credit
                && l.CreatedAt >= lastMonthStart
                && l.CreatedAt < thisMonthStart)
            .SumAsync(l => l.Amount, cancellationToken);

        return Result<PlatformRevenueSummaryDto>.Success(new PlatformRevenueSummaryDto(
            totalRevenue,
            totalRefundsIssued,
            netRevenue,
            totalMentorEarnings,
            totalGrossVolume,
            totalOrders,
            totalRefunds,
            thisMonthRevenue,
            lastMonthRevenue));
    }
}
