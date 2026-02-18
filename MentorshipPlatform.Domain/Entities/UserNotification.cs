using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class UserNotification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? GroupKey { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private UserNotification() { }

    public static UserNotification Create(
        Guid userId, string type, string title, string message,
        string? referenceType = null, Guid? referenceId = null, string? groupKey = null)
    {
        return new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            GroupKey = groupKey,
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
