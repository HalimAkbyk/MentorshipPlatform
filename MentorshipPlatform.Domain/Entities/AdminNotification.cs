using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class AdminNotification : BaseEntity
{
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }

    private AdminNotification() { }

    public static AdminNotification Create(string type, string title, string message, string? referenceType = null, Guid? referenceId = null)
    {
        return new AdminNotification
        {
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
