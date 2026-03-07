using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class MentorReviewNote : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string SenderRole { get; private set; } = string.Empty; // "Admin" or "Mentor"
    public string Message { get; private set; } = string.Empty;

    private MentorReviewNote() { }

    public static MentorReviewNote Create(Guid mentorUserId, Guid senderUserId, string senderRole, string message)
    {
        return new MentorReviewNote
        {
            MentorUserId = mentorUserId,
            SenderUserId = senderUserId,
            SenderRole = senderRole,
            Message = message
        };
    }
}
