using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Earnings.Queries.GetMentorEarningsSummary;

public record MentorEarningsSummaryDto(
    decimal TotalEarnings,
    decimal AvailableBalance,
    decimal EscrowBalance,
    decimal TotalPaidOut,
    decimal ThisMonthEarnings,
    int TotalTransactions);

public record GetMentorEarningsSummaryQuery : IRequest<Result<MentorEarningsSummaryDto>>;

public class GetMentorEarningsSummaryQueryHandler
    : IRequestHandler<GetMentorEarningsSummaryQuery, Result<MentorEarningsSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMentorEarningsSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MentorEarningsSummaryDto>> Handle(
        GetMentorEarningsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<MentorEarningsSummaryDto>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        var entries = await _context.LedgerEntries
            .AsNoTracking()
            .Where(l => l.AccountOwnerUserId == userId)
            .ToListAsync(cancellationToken);

        // MentorAvailable balance = Credits - Debits
        var availableCredits = entries
            .Where(l => l.AccountType == LedgerAccountType.MentorAvailable && l.Direction == LedgerDirection.Credit)
            .Sum(l => l.Amount);
        var availableDebits = entries
            .Where(l => l.AccountType == LedgerAccountType.MentorAvailable && l.Direction == LedgerDirection.Debit)
            .Sum(l => l.Amount);
        var availableBalance = availableCredits - availableDebits;

        // MentorEscrow balance = Credits - Debits (pending sessions)
        var escrowCredits = entries
            .Where(l => l.AccountType == LedgerAccountType.MentorEscrow && l.Direction == LedgerDirection.Credit)
            .Sum(l => l.Amount);
        var escrowDebits = entries
            .Where(l => l.AccountType == LedgerAccountType.MentorEscrow && l.Direction == LedgerDirection.Debit)
            .Sum(l => l.Amount);
        var escrowBalance = escrowCredits - escrowDebits;

        // Total paid out
        var totalPaidOut = entries
            .Where(l => l.AccountType == LedgerAccountType.MentorPayout && l.Direction == LedgerDirection.Credit)
            .Sum(l => l.Amount);

        // Total earnings = all Credits to MentorAvailable (lifetime)
        var totalEarnings = availableCredits;

        // This month earnings
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var thisMonthEarnings = entries
            .Where(l => l.AccountType == LedgerAccountType.MentorAvailable
                     && l.Direction == LedgerDirection.Credit
                     && l.CreatedAt >= monthStart)
            .Sum(l => l.Amount);

        // Total transactions count (all mentor-related entries)
        var totalTransactions = entries.Count;

        return Result<MentorEarningsSummaryDto>.Success(new MentorEarningsSummaryDto(
            totalEarnings,
            availableBalance,
            escrowBalance,
            totalPaidOut,
            thisMonthEarnings,
            totalTransactions));
    }
}
