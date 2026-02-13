using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Domain.Events;
using MentorshipPlatform.Domain.Exceptions;

namespace MentorshipPlatform.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid StudentUserId { get; private set; }
    public Guid MentorUserId { get; private set; }
    public Guid OfferingId { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public int DurationMin { get; private set; }
    public BookingStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }

    public User Student { get; private set; } = null!;
    public User Mentor { get; private set; } = null!;
    public Offering Offering { get; private set; } = null!;

    // private readonly List<BookingNote> _notes = new();
    // public IReadOnlyCollection<BookingNote> Notes => _notes.AsReadOnly();

    private Booking() { }

    public static Booking Create(
        Guid studentUserId,
        Guid mentorUserId,
        Guid offeringId,
        DateTime startAt,
        int durationMin)
    {
        return new Booking
        {
            StudentUserId = studentUserId,
            MentorUserId = mentorUserId,
            OfferingId = offeringId,
            StartAt = startAt,
            EndAt = startAt.AddMinutes(durationMin),
            DurationMin = durationMin,
            Status = BookingStatus.PendingPayment
        };
    }

    public void Confirm()
    {
        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmedEvent(Id, StudentUserId, MentorUserId, StartAt));
    }

    public void Complete()
    {
        Status = BookingStatus.Completed;
        AddDomainEvent(new BookingCompletedEvent(Id, MentorUserId));
    }

    public void Cancel(string reason)
    {
        if (Status != BookingStatus.Confirmed && Status != BookingStatus.Disputed && Status != BookingStatus.PendingPayment)
            throw new DomainException("Only confirmed, disputed or pending-payment bookings can be cancelled");

        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        AddDomainEvent(new BookingCancelledEvent(Id, StartAt, reason));
    }

    public void MarkAsNoShow()
    {
        if (Status != BookingStatus.Confirmed)
            throw new DomainException("Only confirmed bookings can be marked as no-show");

        Status = BookingStatus.NoShow;
    }

    public void MarkAsExpired()
    {
        if (Status != BookingStatus.PendingPayment)
            throw new DomainException("Only pending-payment bookings can expire");

        Status = BookingStatus.Expired;
    }

    public void Dispute(string reason)
    {
        if (Status != BookingStatus.Completed && Status != BookingStatus.NoShow)
            throw new DomainException("Only completed or no-show bookings can be disputed");

        Status = BookingStatus.Disputed;
        CancellationReason = reason; // reuse field for dispute reason
    }

    public decimal CalculateRefundPercentage()
    {
        var hoursUntilStart = (StartAt - DateTime.UtcNow).TotalHours;

        if (hoursUntilStart >= 24) return 1.0m; // 100%
        if (hoursUntilStart >= 2) return 0.5m;  // 50%
        return 0m; // No refund
    }
}