using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class PayoutRequest : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public PayoutRequestStatus Status { get; private set; }
    public string? MentorNote { get; private set; }
    public string? AdminNote { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private PayoutRequest() { }

    public static PayoutRequest Create(Guid mentorUserId, decimal amount, string? mentorNote = null)
    {
        return new PayoutRequest
        {
            MentorUserId = mentorUserId,
            Amount = amount,
            Status = PayoutRequestStatus.Pending,
            MentorNote = mentorNote
        };
    }

    public void Approve(Guid adminUserId, string? adminNote = null)
    {
        Status = PayoutRequestStatus.Approved;
        ProcessedByUserId = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        AdminNote = adminNote;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid adminUserId, string? adminNote = null)
    {
        Status = PayoutRequestStatus.Rejected;
        ProcessedByUserId = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        AdminNote = adminNote;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(Guid adminUserId, string? adminNote = null)
    {
        Status = PayoutRequestStatus.Completed;
        ProcessedByUserId = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        if (adminNote != null) AdminNote = adminNote;
        UpdatedAt = DateTime.UtcNow;
    }
}
