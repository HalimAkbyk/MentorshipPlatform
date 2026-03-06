using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class ExpireCreditJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ExpireCreditJob> _logger;

    public ExpireCreditJob(
        IApplicationDbContext context,
        ILogger<ExpireCreditJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var now = DateTime.UtcNow;

            var expiredCredits = await _context.StudentCredits
                .Where(c => c.ExpiresAt.HasValue
                    && c.ExpiresAt.Value <= now
                    && (c.TotalCredits - c.UsedCredits) > 0)
                .ToListAsync();

            if (!expiredCredits.Any()) return;

            _logger.LogInformation("Found {Count} expired credit records to process", expiredCredits.Count);

            foreach (var credit in expiredCredits)
            {
                var remainingBeforeExpiry = credit.RemainingCredits;
                credit.Expire();

                var transaction = CreditTransaction.Create(
                    credit.Id,
                    CreditTransactionType.Expiry,
                    remainingBeforeExpiry,
                    description: $"Kredi süresi doldu. {remainingBeforeExpiry} kredi iptal edildi.");

                _context.CreditTransactions.Add(transaction);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} credit records", expiredCredits.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExpireCreditJob");
        }
    }
}
