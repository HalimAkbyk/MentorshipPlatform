using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class SessionRequest : BaseEntity
{
    public Guid StudentUserId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public Guid OfferingId { get; private set; }
    public DateTime RequestedStartAt { get; private set; }
    public int DurationMin { get; private set; }
    public string? StudentNote { get; private set; }
    public SessionRequestStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewerRole { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? BookingId { get; private set; }

    // Navigation
    public User Student { get; private set; } = null!;
    public User Mentor { get; private set; } = null!;
    public Offering Offering { get; private set; } = null!;
    public Booking? Booking { get; private set; }

    private SessionRequest() { }

    public static SessionRequest Create(
        Guid studentUserId,
        Guid mentorUserId,
        Guid offeringId,
        DateTime requestedStartAt,
        int durationMin,
        string? studentNote = null)
    {
        return new SessionRequest
        {
            StudentUserId = studentUserId,
            MentorUserId = mentorUserId,
            OfferingId = offeringId,
            RequestedStartAt = requestedStartAt,
            DurationMin = durationMin,
            StudentNote = studentNote,
            Status = SessionRequestStatus.Pending
        };
    }

    public void Approve(Guid reviewedByUserId, string reviewerRole, Guid bookingId)
    {
        Status = reviewerRole == "Admin"
            ? SessionRequestStatus.ApprovedByAdmin
            : SessionRequestStatus.ApprovedByMentor;
        ReviewedByUserId = reviewedByUserId;
        ReviewerRole = reviewerRole;
        BookingId = bookingId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewedByUserId, string reviewerRole, string? reason = null)
    {
        Status = SessionRequestStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        ReviewerRole = reviewerRole;
        RejectionReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsExpired()
    {
        Status = SessionRequestStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsConverted()
    {
        Status = SessionRequestStatus.Converted;
        UpdatedAt = DateTime.UtcNow;
    }
}
