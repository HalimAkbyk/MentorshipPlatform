using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class MentorVerification : BaseEntity
{
    public Guid MentorUserId { get; private set; }

    // ✅ navigation (FK: MentorUserId -> MentorProfile.UserId)
    public MentorProfile? MentorProfile { get; private set; }

    public VerificationType Type { get; private set; }
    public VerificationStatus Status { get; private set; }
    public string? DocumentUrl { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    private MentorVerification() { }

    public static MentorVerification Create(Guid mentorUserId, VerificationType type, string? documentUrl)
    {
        return new MentorVerification
        {
            MentorUserId = mentorUserId,
            Type = type,
            Status = VerificationStatus.Pending,
            DocumentUrl = documentUrl
        };
    }

    public void Approve(string? notes = null)
    {
        Status = VerificationStatus.Approved;
        Notes = notes;
        ReviewedAt = DateTime.UtcNow;
    }

    public void Reject(string notes)
    {
        Status = VerificationStatus.Rejected;
        Notes = notes;
        ReviewedAt = DateTime.UtcNow;
    }
}