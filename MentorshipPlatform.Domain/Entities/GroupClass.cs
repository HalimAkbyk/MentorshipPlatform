using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Domain.Events;

namespace MentorshipPlatform.Domain.Entities;

public class GroupClass : BaseEntity
{
    public Guid MentorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string? CoverImageUrl { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public int Capacity { get; private set; }
    public decimal PricePerSeat { get; private set; }
    public string Currency { get; private set; } = "TRY";
    public ClassStatus Status { get; private set; }

    private readonly List<ClassEnrollment> _enrollments = new();
    public IReadOnlyCollection<ClassEnrollment> Enrollments => _enrollments.AsReadOnly();

    private GroupClass() { }

    public static GroupClass Create(
        Guid mentorUserId,
        string title,
        string? description,
        string category,
        DateTime startAt,
        DateTime endAt,
        int capacity,
        decimal pricePerSeat,
        string? coverImageUrl = null)
    {
        return new GroupClass
        {
            MentorUserId = mentorUserId,
            Title = title,
            Description = description,
            Category = category,
            CoverImageUrl = coverImageUrl,
            StartAt = startAt,
            EndAt = endAt,
            Capacity = capacity,
            PricePerSeat = pricePerSeat,
            Status = ClassStatus.Draft
        };
    }

    public void Publish()
    {
        Status = ClassStatus.Published;
        AddDomainEvent(new GroupClassPublishedEvent(Id, Title, StartAt));
    }

    public void Cancel()
    {
        Status = ClassStatus.Cancelled;
        AddDomainEvent(new GroupClassCancelledEvent(Id, MentorUserId));
    }

    public bool HasAvailableSeats()
    {
        var confirmedCount = _enrollments.Count(e =>
            e.Status == EnrollmentStatus.Confirmed ||
            e.Status == EnrollmentStatus.Attended);
        return confirmedCount < Capacity;
    }

    public int GetConfirmedEnrollmentCount()
    {
        return _enrollments.Count(e =>
            e.Status == EnrollmentStatus.Confirmed ||
            e.Status == EnrollmentStatus.Attended);
    }

    public decimal CalculateRefundPercentage()
    {
        var hoursUntilStart = (StartAt - DateTime.UtcNow).TotalHours;

        if (hoursUntilStart >= 24) return 1.0m; // 100%
        if (hoursUntilStart >= 2) return 0.5m;  // 50%
        return 0m; // No refund
    }

    public void Complete()
    {
        Status = ClassStatus.Completed;
        AddDomainEvent(new GroupClassCompletedEvent(Id, MentorUserId));
    }
}