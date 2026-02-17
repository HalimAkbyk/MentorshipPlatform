using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class ClassEnrollment : BaseEntity
{
    public Guid ClassId { get; private set; }
    public GroupClass Class { get; private set; } = null!;
    public Guid StudentUserId { get; private set; }
    public User StudentUser { get; private set; } = null!;
    public EnrollmentStatus Status { get; private set; }

   
    

    private ClassEnrollment() { }

    public static ClassEnrollment Create(Guid classId, Guid studentUserId)
    {
        return new ClassEnrollment
        {
            ClassId = classId,
            StudentUserId = studentUserId,
            Status = EnrollmentStatus.PendingPayment
        };
    }

    public void Confirm() => Status = EnrollmentStatus.Confirmed;
    public void Cancel() => Status = EnrollmentStatus.Cancelled;
    public void MarkAttended() => Status = EnrollmentStatus.Attended;
    public void Refund() => Status = EnrollmentStatus.Refunded;
}