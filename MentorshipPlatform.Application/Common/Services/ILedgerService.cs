using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Common.Services;

public interface ILedgerService
{
    Task<decimal> GetMentorAvailableBalanceAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    Task<decimal> GetMentorEscrowBalanceAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
}

public class LedgerService : ILedgerService
{
    private readonly IApplicationDbContext _context;

    public LedgerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetMentorAvailableBalanceAsync(
        Guid mentorUserId,
        CancellationToken cancellationToken = default)
    {
        var balance = await _context.LedgerEntries
            .Where(l =>
                l.AccountType == LedgerAccountType.MentorAvailable &&
                l.AccountOwnerUserId == mentorUserId)
            .SumAsync(l => l.Direction == LedgerDirection.Credit ? l.Amount : -l.Amount,
                cancellationToken);

        return balance;
    }

    public async Task<decimal> GetMentorEscrowBalanceAsync(
        Guid mentorUserId,
        CancellationToken cancellationToken = default)
    {
        var balance = await _context.LedgerEntries
            .Where(l =>
                l.AccountType == LedgerAccountType.MentorEscrow &&
                l.AccountOwnerUserId == mentorUserId)
            .SumAsync(l => l.Direction == LedgerDirection.Credit ? l.Amount : -l.Amount,
                cancellationToken);

        return balance;
    }
}