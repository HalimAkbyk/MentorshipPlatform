using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Domain.Entities;

public class AvailabilitySlot : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public bool IsBooked { get; private set; }
    public Guid? RecurrenceId { get; private set; }

    private AvailabilitySlot() { }

    public static AvailabilitySlot Create(Guid mentorUserId, DateTime startAt, DateTime endAt)
    {
        if (startAt >= endAt)
            throw new DomainException("Start time must be before end time");
        startAt = DateTime.SpecifyKind(startAt, DateTimeKind.Utc);
        endAt   = DateTime.SpecifyKind(endAt, DateTimeKind.Utc);

        return new AvailabilitySlot
        {
            MentorUserId = mentorUserId,
            StartAt = startAt,
            EndAt = endAt,
            IsBooked = false
        };
    }

    public void MarkAsBooked() => IsBooked = true;
    public void MarkAsAvailable() => IsBooked = false;
}