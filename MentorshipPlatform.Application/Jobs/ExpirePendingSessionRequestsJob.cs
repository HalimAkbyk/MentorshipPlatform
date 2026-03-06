using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class ExpirePendingSessionRequestsJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<ExpirePendingSessionRequestsJob> _logger;

    private const int ExpiryHours = 48;

    public ExpirePendingSessionRequestsJob(
        IApplicationDbContext context,
        ILogger<ExpirePendingSessionRequestsJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Execute()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-ExpiryHours);

            var expiredRequests = await _context.SessionRequests
                .Where(sr => sr.Status == SessionRequestStatus.Pending && sr.CreatedAt < cutoff)
                .ToListAsync();

            if (!expiredRequests.Any()) return;

            _logger.LogInformation("Found {Count} expired pending session requests", expiredRequests.Count);

            foreach (var request in expiredRequests)
            {
                request.MarkAsExpired();
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} pending session requests", expiredRequests.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExpirePendingSessionRequestsJob");
        }
    }
}
