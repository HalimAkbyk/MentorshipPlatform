using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Enums;

namespace MentorshipPlatform.Domain.Entities;

public class FreeSession : BaseEntity
{
    public Guid MentorUserId { get; set; }
    public Guid StudentUserId { get; set; }
    public Guid? CreditTransactionId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public FreeSessionStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Note { get; set; }

    // Navigation
    public User Mentor { get; set; } = null!;
    public User Student { get; set; } = null!;
    public CreditTransaction? CreditTransaction { get; set; }

    public static FreeSession Create(Guid mentorUserId, Guid studentUserId, string roomName, string? note = null)
    {
        return new FreeSession
        {
            Id = Guid.NewGuid(),
            MentorUserId = mentorUserId,
            StudentUserId = studentUserId,
            RoomName = roomName,
            Status = FreeSessionStatus.Created,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void SetCreditTransaction(Guid creditTransactionId)
    {
        CreditTransactionId = creditTransactionId;
    }

    public void Start()
    {
        Status = FreeSessionStatus.Active;
        StartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void End()
    {
        Status = FreeSessionStatus.Ended;
        EndedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = FreeSessionStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}
