using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Infrastructure.Services;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminNotificationService> _logger;

    public AdminNotificationService(IApplicationDbContext context, ILogger<AdminNotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateOrUpdateGroupedAsync(
        string type,
        string groupKey,
        Func<int, (string title, string message)> messageFactory,
        string? referenceType = null,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check for existing unread notification with same groupKey
            var existing = await _context.AdminNotifications
                .FirstOrDefaultAsync(n => n.GroupKey == groupKey && !n.IsRead, cancellationToken);

            if (existing != null)
            {
                var newCount = existing.Count + 1;
                var (newTitle, newMessage) = messageFactory(newCount);
                existing.UpdateGroupedMessage(newTitle, newMessage, newCount);
            }
            else
            {
                var (title, message) = messageFactory(1);
                var notification = AdminNotification.Create(type, title, message, referenceType, referenceId, groupKey);
                _context.AdminNotifications.Add(notification);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/update admin notification for group {GroupKey}", groupKey);
        }
    }
}
