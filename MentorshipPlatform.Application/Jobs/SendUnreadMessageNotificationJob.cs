using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Application.Jobs;

public class SendUnreadMessageNotificationJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendUnreadMessageNotificationJob> _logger;

    public SendUnreadMessageNotificationJob(
        IApplicationDbContext context,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<SendUnreadMessageNotificationJob> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Execute()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);

        // Find unread messages older than 10 minutes, grouped by (recipient, booking)
        var unreadGroups = await _context.Messages
            .AsNoTracking()
            .Where(m => !m.IsRead && m.CreatedAt <= cutoff)
            .GroupBy(m => new { m.BookingId, m.SenderUserId })
            .Select(g => new
            {
                g.Key.BookingId,
                SenderUserId = g.Key.SenderUserId,
                UnreadCount = g.Count(),
                OldestUnread = g.Min(m => m.CreatedAt)
            })
            .ToListAsync();

        if (!unreadGroups.Any())
            return;

        // For each group, determine the recipient (the other participant)
        var bookingIds = unreadGroups.Select(g => g.BookingId).Distinct().ToList();
        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Where(b => bookingIds.Contains(b.Id))
            .ToListAsync();

        var bookingMap = bookings.ToDictionary(b => b.Id);

        // Get sender names
        var senderIds = unreadGroups.Select(g => g.SenderUserId).Distinct().ToList();
        var senders = await _context.Users
            .AsNoTracking()
            .Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        // Get offering titles
        var offeringIds = bookings.Select(b => b.OfferingId).Distinct().ToList();
        var offerings = await _context.Offerings
            .AsNoTracking()
            .Where(o => offeringIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Title);

        // Check recent notification logs (last 24h) to avoid spam
        var recentLogs = await _context.MessageNotificationLogs
            .AsNoTracking()
            .Where(l => l.SentAt >= DateTime.UtcNow.AddHours(-24)
                        && bookingIds.Contains(l.BookingId))
            .ToListAsync();

        var recentLogSet = recentLogs
            .Select(l => $"{l.BookingId}:{l.RecipientUserId}")
            .ToHashSet();

        var frontendUrl = _configuration["Frontend:BaseUrl"]
                          ?? _configuration["FrontendUrl"]
                          ?? "http://localhost:3000";

        foreach (var group in unreadGroups)
        {
            if (!bookingMap.TryGetValue(group.BookingId, out var booking))
                continue;

            // Recipient is the other participant (not the sender)
            var recipientUserId = booking.StudentUserId == group.SenderUserId
                ? booking.MentorUserId
                : booking.StudentUserId;

            var recipient = booking.StudentUserId == recipientUserId
                ? booking.Student
                : booking.Mentor;

            if (recipient?.Email == null)
                continue;

            var logKey = $"{group.BookingId}:{recipientUserId}";
            if (recentLogSet.Contains(logKey))
                continue;

            senders.TryGetValue(group.SenderUserId, out var senderName);
            offerings.TryGetValue(booking.OfferingId, out var offeringTitle);

            // Determine messages URL based on recipient role
            var isMentor = booking.MentorUserId == recipientUserId;
            var messagesUrl = $"{frontendUrl}/{(isMentor ? "mentor" : "student")}/messages";

            try
            {
                await _emailService.SendUnreadMessageNotificationAsync(
                    recipient.Email,
                    senderName ?? "Bilinmeyen",
                    offeringTitle ?? "Ders",
                    group.UnreadCount,
                    messagesUrl);

                // Log notification
                var log = MessageNotificationLog.Create(group.BookingId, recipientUserId, group.UnreadCount);
                _context.MessageNotificationLogs.Add(log);

                _logger.LogInformation(
                    "Sent unread message notification to {Email} for booking {BookingId} ({Count} messages)",
                    recipient.Email, group.BookingId, group.UnreadCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send unread message notification for booking {BookingId}", group.BookingId);
            }
        }

        await _context.SaveChangesAsync(default);
    }
}
