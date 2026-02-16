using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class RefundRequest : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public RefundRequestStatus Status { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string? AdminNotes { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public RefundType Type { get; private set; }

    // Navigation
    public Order Order { get; private set; } = null!;

    private RefundRequest() { }

    public static RefundRequest Create(
        Guid orderId,
        Guid requestedBy,
        string reason,
        decimal amount,
        RefundType type = RefundType.StudentRequest)
    {
        return new RefundRequest
        {
            OrderId = orderId,
            RequestedByUserId = requestedBy,
            Reason = reason,
            RequestedAmount = amount,
            Status = RefundRequestStatus.Pending,
            Type = type
        };
    }

    public void Approve(decimal approvedAmount, string? adminNotes, Guid processedBy)
    {
        ApprovedAmount = approvedAmount;
        AdminNotes = adminNotes;
        ProcessedByUserId = processedBy;
        ProcessedAt = DateTime.UtcNow;
        Status = RefundRequestStatus.Approved;
    }

    public void Reject(string? adminNotes, Guid processedBy)
    {
        AdminNotes = adminNotes;
        ProcessedByUserId = processedBy;
        ProcessedAt = DateTime.UtcNow;
        Status = RefundRequestStatus.Rejected;
    }
}
