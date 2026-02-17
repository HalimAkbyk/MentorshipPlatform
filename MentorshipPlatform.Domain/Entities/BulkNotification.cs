using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class BulkNotification : BaseEntity
{
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string TargetAudience { get; private set; } = "All";    // All, Students, Mentors
    public string Channel { get; private set; } = "Email";          // Email, InApp
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public int RecipientCount { get; private set; }
    public string Status { get; private set; } = "Draft";           // Draft, Scheduled, Sending, Sent, Failed
    public Guid SentByUserId { get; private set; }

    private BulkNotification() { }

    public static BulkNotification Create(string subject, string body, string targetAudience, string channel, DateTime? scheduledAt, Guid sentByUserId)
    {
        return new BulkNotification
        {
            Subject = subject,
            Body = body,
            TargetAudience = targetAudience,
            Channel = channel,
            ScheduledAt = scheduledAt,
            SentByUserId = sentByUserId,
            Status = scheduledAt.HasValue ? "Scheduled" : "Draft"
        };
    }

    public void MarkAsSending(int recipientCount)
    {
        Status = "Sending";
        RecipientCount = recipientCount;
    }

    public void MarkAsSent()
    {
        Status = "Sent";
        SentAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = "Failed";
    }
}
