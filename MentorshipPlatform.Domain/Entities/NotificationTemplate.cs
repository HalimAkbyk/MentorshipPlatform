using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class NotificationTemplate : BaseEntity
{
    public string Key { get; private set; } = string.Empty;        // unique: "welcome", "booking_confirmation", etc.
    public string Name { get; private set; } = string.Empty;       // display name
    public string Subject { get; private set; } = string.Empty;    // email subject
    public string Body { get; private set; } = string.Empty;       // HTML body
    public string? Variables { get; private set; }                  // JSON: ["displayName", "bookingDate", ...]
    public string Channel { get; private set; } = "Email";         // Email, InApp, SMS
    public bool IsActive { get; private set; } = true;

    private NotificationTemplate() { }

    public static NotificationTemplate Create(string key, string name, string subject, string body, string? variables, string channel = "Email")
    {
        return new NotificationTemplate
        {
            Key = key,
            Name = name,
            Subject = subject,
            Body = body,
            Variables = variables,
            Channel = channel
        };
    }

    public void Update(string name, string subject, string body, string? variables)
    {
        Name = name;
        Subject = subject;
        Body = body;
        Variables = variables;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
