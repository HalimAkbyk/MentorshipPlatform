using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class MessageReport : BaseEntity
{
    public Guid MessageId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public ReportStatus Status { get; private set; }
    public string? AdminNotes { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }

    // Navigation
    public Message Message { get; private set; } = null!;
    public User ReporterUser { get; private set; } = null!;

    private MessageReport() { }

    public static MessageReport Create(Guid messageId, Guid reporterUserId, string reason)
    {
        return new MessageReport
        {
            MessageId = messageId,
            ReporterUserId = reporterUserId,
            Reason = reason,
            Status = ReportStatus.Pending
        };
    }

    public void Review(ReportStatus status, string? adminNotes, Guid reviewedBy)
    {
        Status = status;
        AdminNotes = adminNotes;
        ReviewedAt = DateTime.UtcNow;
        ReviewedByUserId = reviewedBy;
    }
}
